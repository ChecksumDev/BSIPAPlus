#nullable enable
using IPA.Config.Data;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Boolean = IPA.Config.Data.Boolean;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        // emit takes no args, leaves Value at top of stack
        private static void EmitSerializeMember(ILGenerator il, SerializedMemberInfo member, LocalAllocator GetLocal,
            Action<ILGenerator> thisarg, Action<ILGenerator> parentobj)
        {
            EmitLoad(il, member, thisarg);

            using AllocatedLocal valueTypeLocal =
                member.IsNullable
                    ? GetLocal.Allocate(member.Type)
                    : default;

            if (member.IsNullable)
            {
                il.Emit(OpCodes.Stloc, valueTypeLocal.Local);
                il.Emit(OpCodes.Ldloca, valueTypeLocal.Local);
            }

            Label endSerialize = il.DefineLabel();

            if (member.AllowNull)
            {
                Label passedNull = il.DefineLabel();

                il.Emit(OpCodes.Dup);
                if (member.IsNullable)
                {
                    il.Emit(OpCodes.Call, member.Nullable_HasValue.GetGetMethod());
                }

                il.Emit(OpCodes.Brtrue, passedNull);

                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Br, endSerialize);

                il.MarkLabel(passedNull);
            }

            if (member.IsNullable)
            {
                il.Emit(OpCodes.Call, member.Nullable_Value.GetGetMethod());
            }

            Type? memberConversionType = member.ConversionType;
            Type? targetType = GetExpectedValueTypeForType(memberConversionType);
            if (member.HasConverter)
            {
                using AllocatedLocal stlocal = GetLocal.Allocate(memberConversionType);
                using AllocatedLocal valLocal = GetLocal.Allocate(typeof(Value));

                il.Emit(OpCodes.Stloc, stlocal);
                il.BeginExceptionBlock();
                il.Emit(OpCodes.Ldsfld, member.ConverterField);
                il.Emit(OpCodes.Ldloc, stlocal);

                if (member.IsGenericConverter)
                {
                    MethodInfo? toValueBase = member.ConverterBase.GetMethod(nameof(ValueConverter<int>.ToValue),
                        new[] { member.ConverterTarget, typeof(object) });
                    MethodInfo? toValue = member.Converter
                        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => m.GetBaseDefinition() == toValueBase) ?? toValueBase;
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, toValue);
                }
                else
                {
                    MethodInfo? toValueBase = typeof(IValueConverter).GetMethod(nameof(IValueConverter.ToValue),
                        new[] { typeof(object), typeof(object) });
                    MethodInfo? toValue = member.Converter
                        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => m.GetBaseDefinition() == toValueBase) ?? toValueBase;
                    il.Emit(OpCodes.Box);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, toValue);
                }

                il.Emit(OpCodes.Stloc, valLocal);
                il.BeginCatchBlock(typeof(Exception));
                EmitWarnException(il, "Error serializing member using converter");
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Stloc, valLocal);
                il.EndExceptionBlock();
                il.Emit(OpCodes.Ldloc, valLocal);
            }
            else if (targetType == typeof(Text))
            {
                // only happens when arg is a string or char
                MethodInfo? TextCreate = typeof(Value).GetMethod(nameof(Value.Text));
                if (member.Type == typeof(char))
                {
                    MethodInfo? strFromChar = typeof(char).GetMethod(nameof(char.ToString), new[] { typeof(char) });
                    il.Emit(OpCodes.Call, strFromChar);
                }

                il.Emit(OpCodes.Call, TextCreate);
            }
            else if (targetType == typeof(Boolean))
            {
                MethodInfo? BoolCreate = typeof(Value).GetMethod(nameof(Value.Bool));
                il.Emit(OpCodes.Call, BoolCreate);
            }
            else if (targetType == typeof(Integer))
            {
                MethodInfo? IntCreate = typeof(Value).GetMethod(nameof(Value.Integer));
                EmitNumberConvertTo(il, IntCreate.GetParameters()[0].ParameterType, member.Type);
                il.Emit(OpCodes.Call, IntCreate);
            }
            else if (targetType == typeof(FloatingPoint))
            {
                MethodInfo? FloatCreate = typeof(Value).GetMethod(nameof(Value.Float));
                EmitNumberConvertTo(il, FloatCreate.GetParameters()[0].ParameterType, member.Type);
                il.Emit(OpCodes.Call, FloatCreate);
            }
            else if (targetType == typeof(List))
            {
                // TODO: impl this (enumerables)
                Logger.Config.Warn($"Implicit conversions to {targetType} are not currently implemented");
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
            }
            else if (targetType == typeof(Map))
            {
                // TODO: support other aggregate types
                if (!memberConversionType.IsValueType)
                {
                    // if it is a reference type, we assume that its a generated type implementing IGeneratedStore
                    MethodInfo? IGeneratedStore_Serialize =
                        typeof(IGeneratedStore).GetMethod(nameof(IGeneratedStore.Serialize));
                    MethodInfo? IGeneratedStoreT_CopyFrom = typeof(IGeneratedStore<>).MakeGenericType(member.Type)
                        .GetMethod(nameof(IGeneratedStore<object>.CopyFrom));

                    if (!member.IsVirtual)
                    {
                        Label noCreate = il.DefineLabel();
                        using AllocatedLocal stlocal = GetLocal.Allocate(member.Type);

                        // first check to make sure that this is an IGeneratedStore, because we don't control assignments to it
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Isinst, typeof(IGeneratedStore));
                        il.Emit(OpCodes.Brtrue_S, noCreate);
                        il.Emit(OpCodes.Stloc, stlocal);
                        EmitCreateChildGenerated(il, member.Type, parentobj);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Ldloc, stlocal);
                        il.Emit(OpCodes.Ldc_I4_0);
                        il.Emit(OpCodes.Callvirt, IGeneratedStoreT_CopyFrom);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Stloc, stlocal);
                        EmitStore(il, member, il => il.Emit(OpCodes.Ldloc, stlocal), thisarg);
                        il.MarkLabel(noCreate);
                    }

                    il.Emit(OpCodes.Callvirt, IGeneratedStore_Serialize);
                }
                else
                {
                    // generate serialization for value types
                    using AllocatedLocal valueLocal = GetLocal.Allocate(memberConversionType);

                    IEnumerable<SerializedMemberInfo>? structure = ReadObjectMembers(memberConversionType);
                    if (!structure.Any())
                    {
                        Logger.Config.Warn(
                            $"Custom value type {memberConversionType.FullName} (when compiling serialization of" +
                            $" {member.Name} on {member.Member.DeclaringType.FullName}) has no accessible members");
                        il.Emit(OpCodes.Pop);
                    }
                    else
                    {
                        il.Emit(OpCodes.Stloc, valueLocal);
                    }

                    EmitSerializeStructure(il, structure, GetLocal, il => il.Emit(OpCodes.Ldloca, valueLocal),
                        parentobj);
                }
            }

            il.MarkLabel(endSerialize);
        }

        private static void EmitSerializeStructure(ILGenerator il, IEnumerable<SerializedMemberInfo> structure,
            LocalAllocator GetLocal, Action<ILGenerator> thisarg, Action<ILGenerator> parentobj)
        {
            MethodInfo? MapCreate = typeof(Value).GetMethod(nameof(Value.Map));
            MethodInfo? MapAdd = typeof(Map).GetMethod(nameof(Map.Add));

            using AllocatedLocal mapLocal = GetLocal.Allocate(typeof(Map));
            using AllocatedLocal valueLocal = GetLocal.Allocate(typeof(Value));

            il.Emit(OpCodes.Call, MapCreate);
            il.Emit(OpCodes.Stloc, mapLocal);

            foreach (SerializedMemberInfo? mem in structure)
            {
                EmitSerializeMember(il, mem, GetLocal, thisarg, parentobj);
                il.Emit(OpCodes.Stloc, valueLocal);
                il.Emit(OpCodes.Ldloc, mapLocal);
                il.Emit(OpCodes.Ldstr, mem.Name);
                il.Emit(OpCodes.Ldloc, valueLocal);
                il.Emit(OpCodes.Call, MapAdd);
            }

            il.Emit(OpCodes.Ldloc, mapLocal);
        }
    }
}
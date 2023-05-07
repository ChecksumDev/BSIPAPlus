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
        private static void EmitDeserializeGeneratedValue(ILGenerator il, SerializedMemberInfo member, Type srcType,
            LocalAllocator GetLocal,
            Action<ILGenerator> thisarg, Action<ILGenerator> parentobj)
        {
            MethodInfo? IGeneratedStore_Deserialize =
                typeof(IGeneratedStore).GetMethod(nameof(IGeneratedStore.Deserialize));

            using AllocatedLocal valuel = GetLocal.Allocate(srcType);
            Label noCreate = il.DefineLabel();

            il.Emit(OpCodes.Stloc, valuel);
            EmitLoad(il, member, thisarg);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Isinst, typeof(IGeneratedStore));
            il.Emit(OpCodes.Brtrue_S, noCreate);
            il.Emit(OpCodes.Pop);
            EmitCreateChildGenerated(il, member.Type, parentobj);
            il.MarkLabel(noCreate);

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldloc, valuel);
            il.Emit(OpCodes.Callvirt, IGeneratedStore_Deserialize);
        }

        private static void EmitDeserializeNullable(ILGenerator il, SerializedMemberInfo member, Type expected,
            LocalAllocator GetLocal,
            Action<ILGenerator> thisarg, Action<ILGenerator> parentobj)
        {
            if (!member.IsNullable)
            {
                throw new InvalidOperationException("EmitDeserializeNullable called for non-nullable!");
            }

            thisarg ??= il => il.Emit(OpCodes.Ldarg_0);
            parentobj ??= thisarg;
            EmitDeserializeValue(il, member, member.NullableWrappedType, expected, GetLocal, thisarg, parentobj);
            il.Emit(OpCodes.Newobj, member.Nullable_Construct);
        }

        // top of stack is the Value to deserialize; the type will be as returned from GetExpectedValueTypeForType
        // after, top of stack will be thing to write to field
        private static void EmitDeserializeValue(ILGenerator il, SerializedMemberInfo member, Type targetType,
            Type expected, LocalAllocator GetLocal,
            Action<ILGenerator> thisarg, Action<ILGenerator> parentobj)
        {
            if (typeof(Value).IsAssignableFrom(targetType))
            {
                return; // do nothing
            }

            if (expected == typeof(Text))
            {
                MethodInfo? getter = expected.GetProperty(nameof(Text.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
                if (targetType == typeof(char))
                {
                    MethodInfo?
                        strIndex = typeof(string).GetProperty("Chars")
                            .GetGetMethod(); // string's indexer is specially named Chars
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, strIndex);
                }
            }
            else if (expected == typeof(Boolean))
            {
                MethodInfo? getter = expected.GetProperty(nameof(Boolean.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
            }
            else if (expected == typeof(Integer))
            {
                MethodInfo? getter = expected.GetProperty(nameof(Integer.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
                EmitNumberConvertTo(il, targetType, getter.ReturnType);
            }
            else if (expected == typeof(FloatingPoint))
            {
                MethodInfo? getter = expected.GetProperty(nameof(FloatingPoint.Value)).GetGetMethod();
                il.Emit(OpCodes.Call, getter);
                EmitNumberConvertTo(il, targetType, getter.ReturnType);
            } // TODO: implement stuff for lists and maps of various types (probably call out somewhere else to figure out what to do)
            else if (expected == typeof(Map))
            {
                if (!targetType.IsValueType)
                {
                    EmitDeserializeGeneratedValue(il, member, expected, GetLocal, thisarg, parentobj);
                }
                else
                {
                    using AllocatedLocal mapLocal = GetLocal.Allocate(typeof(Map));
                    using AllocatedLocal resultLocal = GetLocal.Allocate(targetType);
                    using AllocatedLocal valueLocal = GetLocal.Allocate(typeof(Value));

                    IEnumerable<SerializedMemberInfo> structure = ReadObjectMembers(targetType);
                    if (!structure.Any())
                    {
                        Logger.Config.Warn($"Custom value type {targetType.FullName} (when compiling serialization of" +
                                           $" {member.Name} on {member.Member.DeclaringType.FullName}) has no accessible members");
                        il.Emit(OpCodes.Pop);
                        il.Emit(OpCodes.Ldloca, resultLocal);
                        il.Emit(OpCodes.Initobj, targetType);
                    }
                    else
                    {
                        il.Emit(OpCodes.Stloc, mapLocal);

                        EmitLoad(il, member, thisarg);
                        il.Emit(OpCodes.Stloc, resultLocal);

                        EmitDeserializeStructure(il, structure, mapLocal, valueLocal, GetLocal,
                            il => il.Emit(OpCodes.Ldloca, resultLocal), parentobj);
                    }

                    il.Emit(OpCodes.Ldloc, resultLocal);
                }
            }
            else
            {
                Logger.Config.Warn($"Implicit conversions to {expected} are not currently implemented");
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
            }
        }

        private static void EmitDeserializeStructure(ILGenerator il, IEnumerable<SerializedMemberInfo> structure,
            LocalBuilder mapLocal, LocalBuilder valueLocal,
            LocalAllocator GetLocal, Action<ILGenerator> thisobj, Action<ILGenerator> parentobj)
        {
            MethodInfo? Map_TryGetValue = typeof(Map).GetMethod(nameof(Map.TryGetValue));

            // head of stack is Map instance
            foreach (SerializedMemberInfo? mem in structure)
            {
                Label nextLabel = il.DefineLabel();

                Label endErrorLabel = il.DefineLabel();

                il.Emit(OpCodes.Ldloc, mapLocal);
                il.Emit(OpCodes.Ldstr, mem.Name);
                il.Emit(OpCodes.Ldloca_S, valueLocal);
                il.Emit(OpCodes.Call, Map_TryGetValue);
                il.Emit(OpCodes.Brtrue_S, endErrorLabel);

                EmitLogError(il, $"Missing key {mem.Name}", false);
                il.Emit(OpCodes.Br, nextLabel);

                il.MarkLabel(endErrorLabel);

                il.Emit(OpCodes.Ldloc_S, valueLocal);
                EmitDeserializeMember(il, mem, nextLabel, il => il.Emit(OpCodes.Ldloc_S, valueLocal), GetLocal, thisobj,
                    parentobj);

                il.MarkLabel(nextLabel);
            }
        }

        private static void EmitDeserializeConverter(ILGenerator il, SerializedMemberInfo member, Label nextLabel,
            LocalAllocator GetLocal,
            Action<ILGenerator> thisobj, Action<ILGenerator> parentobj)
        {
            if (!member.HasConverter)
            {
                throw new InvalidOperationException("EmitDeserializeConverter called for member without converter");
            }

            using AllocatedLocal stlocal = GetLocal.Allocate(typeof(Value));
            using AllocatedLocal valLocal = GetLocal.Allocate(member.Type);

            il.Emit(OpCodes.Stloc, stlocal);
            il.BeginExceptionBlock();
            il.Emit(OpCodes.Ldsfld, member.ConverterField);
            il.Emit(OpCodes.Ldloc, stlocal);
            parentobj(il);

            if (member.IsGenericConverter)
            {
                MethodInfo? fromValueBase = member.ConverterBase.GetMethod(nameof(ValueConverter<int>.FromValue),
                    new[] { typeof(Value), typeof(object) });
                MethodInfo? fromValue = member.Converter
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.GetBaseDefinition() == fromValueBase) ?? fromValueBase;
                il.Emit(OpCodes.Call, fromValue);
            }
            else
            {
                MethodInfo? fromValueBase = typeof(IValueConverter).GetMethod(nameof(IValueConverter.FromValue),
                    new[] { typeof(Value), typeof(object) });
                MethodInfo? fromValue = member.Converter
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.GetBaseDefinition() == fromValueBase) ?? fromValueBase;
                il.Emit(OpCodes.Call, fromValue);
                if (member.Type.IsValueType)
                {
                    il.Emit(OpCodes.Unbox);
                }
            }

            il.Emit(OpCodes.Stloc, valLocal);
            il.BeginCatchBlock(typeof(Exception));
            EmitWarnException(il, "Error occurred while deserializing");
            il.Emit(OpCodes.Leave, nextLabel);
            il.EndExceptionBlock();
            il.Emit(OpCodes.Ldloc, valLocal);
        }

        // emit takes the value being deserialized, logs on error, leaves nothing on stack
        private static void EmitDeserializeMember(ILGenerator il, SerializedMemberInfo member, Label nextLabel,
            Action<ILGenerator> getValue, LocalAllocator GetLocal,
            Action<ILGenerator> thisobj, Action<ILGenerator> parentobj)
        {
            MethodInfo? Object_GetType = typeof(object).GetMethod(nameof(GetType));

            Label implLabel = il.DefineLabel();
            Label passedTypeCheck = il.DefineLabel();
            Type expectType = GetExpectedValueTypeForType(member.ConversionType);

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brtrue_S, implLabel); // null check

            if (!member.AllowNull)
            {
                il.Emit(OpCodes.Pop);
                EmitLogError(il, $"Member {member.Name} ({member.Type}) not nullable", false,
                    il => EmitTypeof(il, expectType));
                il.Emit(OpCodes.Br, nextLabel);
            }
            else if (member.IsNullable)
            {
                il.Emit(OpCodes.Pop);
                using AllocatedLocal valTLocal = GetLocal.Allocate(member.Type);
                il.Emit(OpCodes.Ldloca, valTLocal);
                il.Emit(OpCodes.Initobj, member.Type);
                EmitStore(il, member, il => il.Emit(OpCodes.Ldloc, valTLocal), thisobj);
                il.Emit(OpCodes.Br, nextLabel);
            }
            else
            {
                il.Emit(OpCodes.Pop);
                EmitStore(il, member, il => il.Emit(OpCodes.Ldnull), thisobj);
                il.Emit(OpCodes.Br, nextLabel);
            }


            if (!member.HasConverter)
            {
                il.MarkLabel(implLabel);
                il.Emit(OpCodes.Isinst, expectType); //replaces on stack
                il.Emit(OpCodes.Dup); // duplicate cloned value
                il.Emit(OpCodes.Brtrue, passedTypeCheck); // null check
            }

            Label errorHandle = il.DefineLabel();

            // special cases to handle coersion between Float and Int
            if (member.HasConverter)
            {
                il.MarkLabel(implLabel);
            }
            else if (expectType == typeof(FloatingPoint))
            {
                Label specialTypeCheck = il.DefineLabel();
                il.Emit(OpCodes.Pop);
                getValue(il);
                il.Emit(OpCodes.Isinst, typeof(Integer)); //replaces on stack
                il.Emit(OpCodes.Dup); // duplicate cloned value
                il.Emit(OpCodes.Brfalse, errorHandle); // null check

                MethodInfo? Integer_CoerceToFloat = typeof(Integer).GetMethod(nameof(Integer.AsFloat));
                il.Emit(OpCodes.Call, Integer_CoerceToFloat);

                il.Emit(OpCodes.Br, passedTypeCheck);
            }
            else if (expectType == typeof(Integer))
            {
                Label specialTypeCheck = il.DefineLabel();
                il.Emit(OpCodes.Pop);
                getValue(il);
                il.Emit(OpCodes.Isinst, typeof(FloatingPoint)); //replaces on stack
                il.Emit(OpCodes.Dup); // duplicate cloned value
                il.Emit(OpCodes.Brfalse, errorHandle); // null check

                MethodInfo? Float_CoerceToInt = typeof(FloatingPoint).GetMethod(nameof(FloatingPoint.AsInteger));
                il.Emit(OpCodes.Call, Float_CoerceToInt);

                il.Emit(OpCodes.Br, passedTypeCheck);
            }

            if (!member.HasConverter)
            {
                il.MarkLabel(errorHandle);
                il.Emit(OpCodes.Pop);
                EmitLogError(il, $"Unexpected type deserializing {member.Name}", false,
                    il => EmitTypeof(il, expectType), il =>
                    {
                        getValue(il);
                        il.Emit(OpCodes.Callvirt, Object_GetType);
                    });
                il.Emit(OpCodes.Br, nextLabel);
            }

            il.MarkLabel(passedTypeCheck);

            using AllocatedLocal local = GetLocal.Allocate(member.Type);
            if (member.HasConverter)
            {
                EmitDeserializeConverter(il, member, nextLabel, GetLocal, thisobj, parentobj);
            }
            else if (member.IsNullable)
            {
                EmitDeserializeNullable(il, member, expectType, GetLocal, thisobj, parentobj);
            }
            else
            {
                EmitDeserializeValue(il, member, member.Type, expectType, GetLocal, thisobj, parentobj);
            }

            il.Emit(OpCodes.Stloc, local);
            EmitStore(il, member, il => il.Emit(OpCodes.Ldloc, local), thisobj);
        }
    }
}
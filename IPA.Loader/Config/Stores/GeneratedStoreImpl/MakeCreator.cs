#nullable enable
using IPA.Config.Data;
using IPA.Config.Stores.Attributes;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        private static void GetMethodThis(ILGenerator il)
        {
            il.Emit(OpCodes.Ldarg_0);
        }

        private static (GeneratedStoreCreator ctor, Type type) MakeCreator(Type type)
        {
            // note that this does not and should not use converters by default for everything
            if (!type.IsClass)
            {
                throw new ArgumentException("Config type is not a class");
            }

            ConstructorInfo? baseCtor = type.GetConstructor(Type.EmptyTypes); // get a default constructor
            if (baseCtor == null)
            {
                throw new ArgumentException("Config type does not have a public parameterless constructor");
            }

            #region Parse base object structure

            const BindingFlags overrideMemberFlags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            MethodInfo? baseChanged = type.GetMethod("Changed", overrideMemberFlags, null, Type.EmptyTypes,
                Array.Empty<ParameterModifier>());
            if (baseChanged != null && IsMethodInvalid(baseChanged, typeof(void)))
            {
                baseChanged = null;
            }

            MethodInfo? baseOnReload = type.GetMethod("OnReload", overrideMemberFlags, null, Type.EmptyTypes,
                Array.Empty<ParameterModifier>());
            if (baseOnReload != null && IsMethodInvalid(baseOnReload, typeof(void)))
            {
                baseOnReload = null;
            }

            MethodInfo? baseCopyFrom = type.GetMethod("CopyFrom", overrideMemberFlags, null, new[] { type },
                Array.Empty<ParameterModifier>());
            if (baseCopyFrom != null && IsMethodInvalid(baseCopyFrom, typeof(void)))
            {
                baseCopyFrom = null;
            }

            MethodInfo? baseChangeTransaction = type.GetMethod("ChangeTransaction", overrideMemberFlags, null,
                Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (baseChangeTransaction != null && IsMethodInvalid(baseChangeTransaction, typeof(IDisposable)))
            {
                baseChangeTransaction = null;
            }

            bool isINotifyPropertyChanged =
                type.FindInterfaces((i, t) => i == (Type)t, typeof(INotifyPropertyChanged)).Length != 0;
            bool hasNotifyAttribute = type.GetCustomAttribute<NotifyPropertyChangesAttribute>() != null;

            IEnumerable<SerializedMemberInfo>? structure = ReadObjectMembers(type);
            if (!structure.Any())
            {
                Logger.Config.Warn($"Custom type {type.FullName} has no accessible members");
            }

            #endregion

            TypeBuilder? typeBuilder = Module.DefineType($"{type.FullName}<Generated>",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, type);

            FieldBuilder? typeField = typeBuilder.DefineField("<>_type", typeof(Type),
                FieldAttributes.Private | FieldAttributes.InitOnly);
            FieldBuilder? implField = typeBuilder.DefineField("<>_impl", typeof(Impl),
                FieldAttributes.Private | FieldAttributes.InitOnly);
            FieldBuilder? parentField = typeBuilder.DefineField("<>_parent", typeof(IGeneratedStore),
                FieldAttributes.Private | FieldAttributes.InitOnly);

            #region Constructor

            ConstructorBuilder? ctor = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard, new[] { typeof(IGeneratedStore) });
            {
                ILGenerator? il = ctor.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // keep this at bottom of stack

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Call, baseCtor);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldarg_1); // load parent
                il.Emit(OpCodes.Stfld, parentField);

                il.Emit(OpCodes.Dup);
                EmitTypeof(il, type);
                il.Emit(OpCodes.Stfld, typeField);

                Label noImplLabel = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Brtrue, noImplLabel);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Newobj, Impl.Ctor);
                il.Emit(OpCodes.Stfld, implField);
                il.MarkLabel(noImplLabel);

                LocalAllocator? GetLocal = MakeLocalAllocator(il);

                foreach (SerializedMemberInfo? member in structure)
                {
                    if (NeedsCorrection(member))
                    {
                        EmitLoadCorrectStore(il, member, false, true, GetLocal, GetMethodThis, GetMethodThis,
                            GetMethodThis);
                    }
                }

                il.Emit(OpCodes.Pop);

                il.Emit(OpCodes.Ret);
            }

            #endregion

            const MethodAttributes propertyMethodAttr =
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            const MethodAttributes virtualPropertyMethodAttr =
                propertyMethodAttr | MethodAttributes.Virtual | MethodAttributes.Final;
            const MethodAttributes virtualMemberMethod = MethodAttributes.Public | MethodAttributes.Virtual |
                                                         MethodAttributes.HideBySig | MethodAttributes.Final;

            #region INotifyPropertyChanged

            MethodBuilder? notifyChanged = null;
            if (isINotifyPropertyChanged || hasNotifyAttribute)
            {
                // we don't actually want to notify if the base class implements it
                if (isINotifyPropertyChanged)
                {
                    MethodInfo? ExistingRaisePropertyChanged = type.GetMethod("RaisePropertyChanged",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                        BindingFlags.FlattenHierarchy,
                        null, new[] { typeof(string) }, Array.Empty<ParameterModifier>());
                    if (ExistingRaisePropertyChanged != null && !ExistingRaisePropertyChanged.IsPrivate)
                    {
                        notifyChanged = typeBuilder.DefineMethod("<>NotifyChanged",
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Final, null,
                            new[] { typeof(string) });

                        {
                            ILGenerator? il = notifyChanged.GetILGenerator();

                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldarg_1);
                            il.Emit(OpCodes.Call, ExistingRaisePropertyChanged);
                            il.Emit(OpCodes.Ret);
                        }
                    }
                    else
                    {
                        Logger.Default.Critical(
                            $"Type '{type.FullName}' implements INotifyPropertyChanged but does not have an accessible " +
                            "'RaisePropertyChanged(string)' method, automatic raising of PropertyChanged event is disabled.");
                    }
                }
                else
                {
                    Type? INotifyPropertyChanged_t = typeof(INotifyPropertyChanged);
                    typeBuilder.AddInterfaceImplementation(INotifyPropertyChanged_t);

                    EventInfo? INotifyPropertyChanged_PropertyChanged =
                        INotifyPropertyChanged_t.GetEvent(nameof(INotifyPropertyChanged.PropertyChanged));

                    Type? PropertyChangedEventHandler_t = typeof(PropertyChangedEventHandler);
                    MethodInfo? PropertyChangedEventHander_Invoke =
                        PropertyChangedEventHandler_t.GetMethod(nameof(PropertyChangedEventHandler.Invoke));

                    Type? PropertyChangedEventArgs_t = typeof(PropertyChangedEventArgs);
                    ConstructorInfo? PropertyChangedEventArgs_ctor =
                        PropertyChangedEventArgs_t.GetConstructor(new[] { typeof(string) });

                    Type? Delegate_t = typeof(Delegate);
                    MethodInfo? Delegate_Combine = Delegate_t.GetMethod(nameof(Delegate.Combine),
                        BindingFlags.Static | BindingFlags.Public, null,
                        new[] { Delegate_t, Delegate_t }, Array.Empty<ParameterModifier>());
                    MethodInfo? Delegate_Remove = Delegate_t.GetMethod(nameof(Delegate.Remove),
                        BindingFlags.Static | BindingFlags.Public, null,
                        new[] { Delegate_t, Delegate_t }, Array.Empty<ParameterModifier>());

                    MethodInfo? CompareExchange = typeof(Interlocked).GetMethods()
                        .Where(m => m.Name == nameof(Interlocked.CompareExchange))
                        .Where(m => m.ContainsGenericParameters)
                        .Where(m => m.GetParameters().Length == 3).First()
                        .MakeGenericMethod(PropertyChangedEventHandler_t);

                    EventInfo? basePropChangedEvent = type.GetEvents()
                        .Where(e => e.GetAddMethod().GetBaseDefinition().DeclaringType == INotifyPropertyChanged_t)
                        .FirstOrDefault();
                    MethodInfo? basePropChangedAdd = basePropChangedEvent?.GetAddMethod();
                    MethodInfo? basePropChangedRemove = basePropChangedEvent?.GetRemoveMethod();

                    FieldBuilder? PropertyChanged_backing = typeBuilder.DefineField("<event>PropertyChanged",
                        PropertyChangedEventHandler_t, FieldAttributes.Private);

                    MethodBuilder? add_PropertyChanged = typeBuilder.DefineMethod("<add>PropertyChanged",
                        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Final |
                        MethodAttributes.Virtual,
                        null, new[] { PropertyChangedEventHandler_t });
                    typeBuilder.DefineMethodOverride(add_PropertyChanged,
                        INotifyPropertyChanged_PropertyChanged.GetAddMethod());
                    if (basePropChangedAdd != null)
                    {
                        typeBuilder.DefineMethodOverride(add_PropertyChanged, basePropChangedAdd);
                    }

                    {
                        ILGenerator? il = add_PropertyChanged.GetILGenerator();

                        Label loopLabel = il.DefineLabel();
                        LocalBuilder? delTemp = il.DeclareLocal(PropertyChangedEventHandler_t);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldfld, PropertyChanged_backing);

                        il.MarkLabel(loopLabel);
                        il.Emit(OpCodes.Stloc, delTemp);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldflda, PropertyChanged_backing);

                        il.Emit(OpCodes.Ldloc, delTemp);
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Call, Delegate_Combine);
                        il.Emit(OpCodes.Castclass, PropertyChangedEventHandler_t);

                        il.Emit(OpCodes.Ldloc, delTemp);
                        il.Emit(OpCodes.Call, CompareExchange);

                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Ldloc, delTemp);
                        il.Emit(OpCodes.Bne_Un_S, loopLabel);

                        il.Emit(OpCodes.Pop);
                        il.Emit(OpCodes.Ret);
                    }

                    MethodBuilder? remove_PropertyChanged = typeBuilder.DefineMethod("<remove>PropertyChanged",
                        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Final |
                        MethodAttributes.Virtual,
                        null, new[] { PropertyChangedEventHandler_t });
                    typeBuilder.DefineMethodOverride(remove_PropertyChanged,
                        INotifyPropertyChanged_PropertyChanged.GetRemoveMethod());
                    if (basePropChangedRemove != null)
                    {
                        typeBuilder.DefineMethodOverride(remove_PropertyChanged, basePropChangedRemove);
                    }

                    {
                        ILGenerator? il = remove_PropertyChanged.GetILGenerator();

                        Label loopLabel = il.DefineLabel();
                        LocalBuilder? delTemp = il.DeclareLocal(PropertyChangedEventHandler_t);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldfld, PropertyChanged_backing);

                        il.MarkLabel(loopLabel);
                        il.Emit(OpCodes.Stloc, delTemp);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldflda, PropertyChanged_backing);

                        il.Emit(OpCodes.Ldloc, delTemp);
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Call, Delegate_Remove);
                        il.Emit(OpCodes.Castclass, PropertyChangedEventHandler_t);

                        il.Emit(OpCodes.Ldloc, delTemp);
                        il.Emit(OpCodes.Call, CompareExchange);

                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Ldloc, delTemp);
                        il.Emit(OpCodes.Bne_Un_S, loopLabel);

                        il.Emit(OpCodes.Pop);
                        il.Emit(OpCodes.Ret);
                    }

                    EventBuilder? PropertyChanged_event = typeBuilder.DefineEvent(
                        nameof(INotifyPropertyChanged.PropertyChanged), EventAttributes.None,
                        PropertyChangedEventHandler_t);
                    PropertyChanged_event.SetAddOnMethod(add_PropertyChanged);
                    PropertyChanged_event.SetRemoveOnMethod(remove_PropertyChanged);

                    notifyChanged = typeBuilder.DefineMethod("<>NotifyChanged",
                        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Final, null,
                        new[] { typeof(string) });

                    {
                        ILGenerator? il = notifyChanged.GetILGenerator();

                        Label invokeNonNull = il.DefineLabel();

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldfld, PropertyChanged_backing);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Brtrue, invokeNonNull);
                        il.Emit(OpCodes.Pop);
                        il.Emit(OpCodes.Ret);

                        il.MarkLabel(invokeNonNull);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Newobj, PropertyChangedEventArgs_ctor);
                        il.Emit(OpCodes.Call, PropertyChangedEventHander_Invoke);
                        il.Emit(OpCodes.Ret);
                    }
                }
            }

            #endregion

            #region IGeneratedStore

            typeBuilder.AddInterfaceImplementation(typeof(IGeneratedStore));

            Type? IGeneratedStore_t = typeof(IGeneratedStore);
            MethodInfo? IGeneratedStore_GetImpl =
                IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Impl)).GetGetMethod();
            MethodInfo? IGeneratedStore_GetType =
                IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Type)).GetGetMethod();
            MethodInfo? IGeneratedStore_GetParent =
                IGeneratedStore_t.GetProperty(nameof(IGeneratedStore.Parent)).GetGetMethod();
            MethodInfo? IGeneratedStore_Serialize = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.Serialize));
            MethodInfo? IGeneratedStore_Deserialize = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.Deserialize));
            MethodInfo? IGeneratedStore_OnReload = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.OnReload));
            MethodInfo? IGeneratedStore_Changed = IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.Changed));
            MethodInfo? IGeneratedStore_ChangeTransaction =
                IGeneratedStore_t.GetMethod(nameof(IGeneratedStore.ChangeTransaction));

            #region IGeneratedStore.OnReload

            MethodBuilder? onReload = typeBuilder.DefineMethod($"<>{nameof(IGeneratedStore.OnReload)}",
                virtualMemberMethod, null, Type.EmptyTypes);
            typeBuilder.DefineMethodOverride(onReload, IGeneratedStore_OnReload);
            if (baseOnReload != null)
            {
                typeBuilder.DefineMethodOverride(onReload, baseOnReload);
            }

            {
                ILGenerator? il = onReload.GetILGenerator();

                if (baseOnReload != null)
                {
                    il.Emit(OpCodes.Ldarg_0); // load this
                    il.Emit(OpCodes.Tailcall);
                    il.Emit(OpCodes.Call, baseOnReload); // load impl field
                }

                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IGeneratedStore.Impl

            PropertyBuilder? implProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Impl),
                PropertyAttributes.None, typeof(Impl), null);
            MethodBuilder? implPropGet = typeBuilder.DefineMethod($"<g>{nameof(IGeneratedStore.Impl)}",
                virtualPropertyMethodAttr, implProp.PropertyType, Type.EmptyTypes);
            implProp.SetGetMethod(implPropGet);
            typeBuilder.DefineMethodOverride(implPropGet, IGeneratedStore_GetImpl);

            {
                ILGenerator? il = implPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, implField); // load impl field
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IGeneratedStore.Type

            PropertyBuilder? typeProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Type),
                PropertyAttributes.None, typeof(Type), null);
            MethodBuilder? typePropGet = typeBuilder.DefineMethod($"<g>{nameof(IGeneratedStore.Type)}",
                virtualPropertyMethodAttr, typeProp.PropertyType, Type.EmptyTypes);
            typeProp.SetGetMethod(typePropGet);
            typeBuilder.DefineMethodOverride(typePropGet, IGeneratedStore_GetType);

            {
                ILGenerator? il = typePropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, typeField); // load impl field
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IGeneratedStore.Parent

            PropertyBuilder? parentProp = typeBuilder.DefineProperty(nameof(IGeneratedStore.Parent),
                PropertyAttributes.None, typeof(IGeneratedStore), null);
            MethodBuilder? parentPropGet = typeBuilder.DefineMethod($"<g>{nameof(IGeneratedStore.Parent)}",
                virtualPropertyMethodAttr, parentProp.PropertyType, Type.EmptyTypes);
            parentProp.SetGetMethod(parentPropGet);
            typeBuilder.DefineMethodOverride(parentPropGet, IGeneratedStore_GetParent);

            {
                ILGenerator? il = parentPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0); // load this
                il.Emit(OpCodes.Ldfld, parentField); // load impl field
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IGeneratedStore.Serialize

            MethodBuilder? serializeGen = typeBuilder.DefineMethod($"<>{nameof(IGeneratedStore.Serialize)}",
                virtualPropertyMethodAttr, IGeneratedStore_Serialize.ReturnType, Type.EmptyTypes);
            typeBuilder.DefineMethodOverride(serializeGen, IGeneratedStore_Serialize);

            {
                // this is non-locking because the only code that will call this will already own the correct lock
                ILGenerator? il = serializeGen.GetILGenerator();

                LocalAllocator? GetLocal = MakeLocalAllocator(il);

                EmitSerializeStructure(il, structure, GetLocal, GetMethodThis, GetMethodThis);

                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IGeneratedStore.Deserialize

            MethodBuilder? deserializeGen = typeBuilder.DefineMethod($"<>{nameof(IGeneratedStore.Deserialize)}",
                virtualPropertyMethodAttr, null,
                new[] { IGeneratedStore_Deserialize.GetParameters()[0].ParameterType });
            typeBuilder.DefineMethodOverride(deserializeGen, IGeneratedStore_Deserialize);

            {
                // this is non-locking because the only code that will call this will already own the correct lock
                ILGenerator? il = deserializeGen.GetILGenerator();

                Type? Map_t = typeof(Map);
                MethodInfo? Map_TryGetValue = Map_t.GetMethod(nameof(Map.TryGetValue));
                MethodInfo? Object_GetType = typeof(object).GetMethod(nameof(GetType));

                LocalBuilder? valueLocal = il.DeclareLocal(typeof(Value));
                LocalBuilder? mapLocal = il.DeclareLocal(typeof(Map));

                Label nonNull = il.DefineLabel();

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Brtrue, nonNull);

                EmitLogError(il, "Attempting to deserialize null", true);
                il.Emit(OpCodes.Ret);

                il.MarkLabel(nonNull);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Isinst, Map_t);
                il.Emit(OpCodes.Dup); // duplicate cloned value
                il.Emit(OpCodes.Stloc, mapLocal);
                Label notMapError = il.DefineLabel();
                il.Emit(OpCodes.Brtrue, notMapError);
                // handle error
                EmitLogError(il, $"Invalid root for deserializing {type.FullName}", true,
                    il => EmitTypeof(il, Map_t), il =>
                    {
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Callvirt, Object_GetType);
                    });
                il.Emit(OpCodes.Ret);

                il.MarkLabel(notMapError);

                LocalAllocator? GetLocal = MakeLocalAllocator(il);

                // head of stack is Map instance
                EmitDeserializeStructure(il, structure, mapLocal, valueLocal, GetLocal, GetMethodThis, GetMethodThis);

                if (notifyChanged != null)
                {
                    foreach (SerializedMemberInfo? member in structure)
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, member.Name);
                        il.Emit(OpCodes.Call, notifyChanged);
                    }
                }

                il.Emit(OpCodes.Ret);
            }

            #endregion

            #endregion

            #region IConfigStore

            typeBuilder.AddInterfaceImplementation(typeof(IConfigStore));

            Type? IConfigStore_t = typeof(IConfigStore);
            MethodInfo? IConfigStore_GetSyncObject =
                IConfigStore_t.GetProperty(nameof(IConfigStore.SyncObject)).GetGetMethod();
            MethodInfo? IConfigStore_GetWriteSyncObject =
                IConfigStore_t.GetProperty(nameof(IConfigStore.WriteSyncObject)).GetGetMethod();
            MethodInfo? IConfigStore_WriteTo = IConfigStore_t.GetMethod(nameof(IConfigStore.WriteTo));
            MethodInfo? IConfigStore_ReadFrom = IConfigStore_t.GetMethod(nameof(IConfigStore.ReadFrom));

            #region IConfigStore.SyncObject

            PropertyBuilder? syncObjProp = typeBuilder.DefineProperty(nameof(IConfigStore.SyncObject),
                PropertyAttributes.None, IConfigStore_GetSyncObject.ReturnType, null);
            MethodBuilder? syncObjPropGet = typeBuilder.DefineMethod($"<g>{nameof(IConfigStore.SyncObject)}",
                virtualPropertyMethodAttr, syncObjProp.PropertyType, Type.EmptyTypes);
            syncObjProp.SetGetMethod(syncObjPropGet);
            typeBuilder.DefineMethodOverride(syncObjPropGet, IConfigStore_GetSyncObject);

            {
                ILGenerator? il = syncObjPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplGetSyncObjectMethod);
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IConfigStore.WriteSyncObject

            PropertyBuilder? writeSyncObjProp = typeBuilder.DefineProperty(nameof(IConfigStore.WriteSyncObject),
                PropertyAttributes.None, IConfigStore_GetWriteSyncObject.ReturnType, null);
            MethodBuilder? writeSyncObjPropGet = typeBuilder.DefineMethod($"<g>{nameof(IConfigStore.WriteSyncObject)}",
                virtualPropertyMethodAttr, writeSyncObjProp.PropertyType, Type.EmptyTypes);
            writeSyncObjProp.SetGetMethod(writeSyncObjPropGet);
            typeBuilder.DefineMethodOverride(writeSyncObjPropGet, IConfigStore_GetWriteSyncObject);

            {
                ILGenerator? il = writeSyncObjPropGet.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplGetWriteSyncObjectMethod);
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IConfigStore.WriteTo

            MethodBuilder? writeTo = typeBuilder.DefineMethod($"<>{nameof(IConfigStore.WriteTo)}", virtualMemberMethod,
                null, new[] { typeof(ConfigProvider) });
            typeBuilder.DefineMethodOverride(writeTo, IConfigStore_WriteTo);

            {
                ILGenerator? il = writeTo.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplWriteToMethod);
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IConfigStore.ReadFrom

            MethodBuilder? readFrom = typeBuilder.DefineMethod($"<>{nameof(IConfigStore.ReadFrom)}",
                virtualMemberMethod, null, new[] { typeof(ConfigProvider) });
            typeBuilder.DefineMethodOverride(readFrom, IConfigStore_ReadFrom);

            {
                ILGenerator? il = readFrom.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplReadFromMethod);
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #endregion

            #region Changed

            MethodBuilder? coreChanged = typeBuilder.DefineMethod(
                "<>Changed",
                virtualMemberMethod,
                null, Type.EmptyTypes);
            typeBuilder.DefineMethodOverride(coreChanged, IGeneratedStore_Changed);
            if (baseChanged != null)
            {
                typeBuilder.DefineMethodOverride(coreChanged, baseChanged);
            }

            {
                ILGenerator? il = coreChanged.GetILGenerator();

                if (baseChanged != null)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, baseChanged); // call base
                }

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplSignalChangedMethod);
                il.Emit(OpCodes.Ret); // simply call our impl's SignalChanged method and return
            }

            #endregion

            #region ChangeTransaction

            MethodBuilder? coreChangeTransaction = typeBuilder.DefineMethod(
                "<>ChangeTransaction",
                virtualMemberMethod,
                typeof(IDisposable), Type.EmptyTypes);
            typeBuilder.DefineMethodOverride(coreChangeTransaction, IGeneratedStore_ChangeTransaction);
            if (baseChangeTransaction != null)
            {
                typeBuilder.DefineMethodOverride(coreChangeTransaction, baseChangeTransaction);
            }

            {
                ILGenerator? il = coreChangeTransaction.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                if (baseChangeTransaction != null)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, baseChangeTransaction);
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                il.Emit(OpCodes.Tailcall);
                il.Emit(OpCodes.Call, Impl.ImplChangeTransactionMethod);
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #region IGeneratedStore<T>

            Type? IGeneratedStore_T_t = typeof(IGeneratedStore<>).MakeGenericType(type);
            typeBuilder.AddInterfaceImplementation(IGeneratedStore_T_t);

            MethodInfo? IGeneratedStore_T_CopyFrom =
                IGeneratedStore_T_t.GetMethod(nameof(IGeneratedStore<Config>.CopyFrom));

            #region IGeneratedStore<T>.CopyFrom

            MethodBuilder? copyFrom = typeBuilder.DefineMethod($"<>{nameof(IGeneratedStore<Config>.CopyFrom)}",
                virtualMemberMethod, null, new[] { type, typeof(bool) });
            typeBuilder.DefineMethodOverride(copyFrom, IGeneratedStore_T_CopyFrom);

            {
                ILGenerator? il = copyFrom.GetILGenerator();

                LocalBuilder? transactionLocal = il.DeclareLocal(IDisposable_t);

                Label startLock = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Brfalse, startLock);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, coreChangeTransaction); // take the write lock
                il.Emit(OpCodes.Stloc, transactionLocal);
                il.MarkLabel(startLock);

                LocalAllocator? GetLocal = MakeLocalAllocator(il);

                foreach (SerializedMemberInfo? member in structure)
                {
                    il.BeginExceptionBlock();

                    EmitLoadCorrectStore(il, member, false, false, GetLocal, il => il.Emit(OpCodes.Ldarg_1),
                        GetMethodThis, GetMethodThis);

                    il.BeginCatchBlock(typeof(Exception));

                    EmitWarnException(il, $"Error while copying from member {member.Name}");

                    il.EndExceptionBlock();
                }

                if (notifyChanged != null)
                {
                    foreach (SerializedMemberInfo? member in structure)
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, member.Name);
                        il.Emit(OpCodes.Call, notifyChanged);
                    }
                }

                Label endLock = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Brfalse, endLock);
                il.Emit(OpCodes.Ldloc, transactionLocal);
                il.Emit(OpCodes.Callvirt, IDisposable_Dispose);
                il.MarkLabel(endLock);
                il.Emit(OpCodes.Ret);
            }

            #endregion

            #endregion

            #region base.CopyFrom

            if (baseCopyFrom != null)
            {
                MethodBuilder? pubCopyFrom = typeBuilder.DefineMethod(
                    baseCopyFrom.Name,
                    virtualMemberMethod,
                    null, new[] { type });
                typeBuilder.DefineMethodOverride(pubCopyFrom, baseCopyFrom);

                {
                    ILGenerator? il = pubCopyFrom.GetILGenerator();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, coreChangeTransaction);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, copyFrom); // call internal

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Call, baseCopyFrom); // call base

                    il.Emit(OpCodes.Tailcall);
                    il.Emit(OpCodes.Callvirt, IDisposable_Dispose); // dispose transaction (which calls changed)
                    il.Emit(OpCodes.Ret);
                }
            }

            #endregion

            #region Members

            foreach (SerializedMemberInfo? member in structure.Where(m => m.IsVirtual))
            {
                // IsVirtual implies !IsField
                PropertyInfo? prop = (PropertyInfo)member.Member;
                MethodInfo? get = prop.GetGetMethod(true);
                MethodInfo? set = prop.GetSetMethod(true);

                PropertyBuilder? propBuilder =
                    typeBuilder.DefineProperty($"{member.Name}#", PropertyAttributes.None, member.Type, null);
                MethodBuilder? propGet = typeBuilder.DefineMethod($"<g>{propBuilder.Name}", virtualPropertyMethodAttr,
                    member.Type, Type.EmptyTypes);
                propBuilder.SetGetMethod(propGet);
                typeBuilder.DefineMethodOverride(propGet, get);

                {
                    ILGenerator? il = propGet.GetILGenerator();

                    LocalBuilder? local = il.DeclareLocal(member.Type);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, Impl.ImplTakeReadMethod); // take the read lock

                    il.BeginExceptionBlock();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, get); // call base getter
                    il.Emit(OpCodes.Stloc, local);

                    il.BeginFinallyBlock();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, Impl.ImplReleaseReadMethod); // release the read lock

                    il.EndExceptionBlock();

                    il.Emit(OpCodes.Ldloc, local);
                    il.Emit(OpCodes.Ret);
                }

                MethodBuilder? propSet = typeBuilder.DefineMethod($"<s>{propBuilder.Name}", virtualPropertyMethodAttr,
                    null, new[] { member.Type });
                propBuilder.SetSetMethod(propSet);
                typeBuilder.DefineMethodOverride(propSet, set);

                {
                    ILGenerator? il = propSet.GetILGenerator();

                    LocalBuilder? transactionLocal = il.DeclareLocal(IDisposable_t);
                    LocalAllocator? GetLocal = MakeLocalAllocator(il);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, coreChangeTransaction); // take the write lock
                    il.Emit(OpCodes.Stloc, transactionLocal);

                    il.BeginExceptionBlock();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);
                    EmitCorrectMember(il, member, false, false, GetLocal, GetMethodThis, GetMethodThis);
                    il.Emit(OpCodes.Call, set);

                    il.BeginFinallyBlock();

                    il.Emit(OpCodes.Ldloc, transactionLocal);
                    il.Emit(OpCodes.Callvirt, IDisposable_Dispose);

                    il.EndExceptionBlock();

                    if (notifyChanged != null)
                    {
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldstr, member.Name);
                        il.Emit(OpCodes.Call, notifyChanged);
                    }

                    il.Emit(OpCodes.Ret);
                }
            }

            #endregion

            Type? genType = typeBuilder.CreateType();

            ParameterExpression? parentParam = Expression.Parameter(typeof(IGeneratedStore), "parent");
            GeneratedStoreCreator? creatorDel = Expression.Lambda<GeneratedStoreCreator>(
                Expression.New(ctor, parentParam), parentParam
            ).Compile();

            return (creatorDel, genType);
        }

        internal delegate IConfigStore GeneratedStoreCreator(IGeneratedStore? parent);
    }
}
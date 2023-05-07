#nullable enable
using IPA.Config.Stores;
using IPA.Utilities;
using IPA.Utilities.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        internal const string GeneratedAssemblyName = "IPA.Config.Generated";

        private static readonly MethodInfo CreateGParent =
            typeof(GeneratedStoreImpl).GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static, null,
                CallingConventions.Any, new[] { typeof(IGeneratedStore) }, Array.Empty<ParameterModifier>());

        private static readonly SingleCreationValueCache<Type, (GeneratedStoreCreator ctor, Type type)>
            generatedCreators = new();

        private static AssemblyBuilder? assembly;

        private static ModuleBuilder? module;

        // TODO: does this need to be a SingleCreationValueCache or similar?
        private static readonly Dictionary<Type, Dictionary<Type, FieldInfo>> TypeRequiredConverters = new();

        private static AssemblyBuilder Assembly
        {
            get
            {
                if (assembly == null)
                {
                    AssemblyName? name = new(GeneratedAssemblyName);
                    assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
                }

                return assembly;
            }
        }

        private static ModuleBuilder Module
        {
            get
            {
                if (module == null)
                {
                    module = Assembly.DefineDynamicModule(Assembly.GetName().Name, Assembly.GetName().Name + ".dll");
                }

                return module;
            }
        }

        public static T Create<T>() where T : class
        {
            return (T)Create(typeof(T));
        }

        public static IConfigStore Create(Type type)
        {
            return Create(type, null);
        }

        internal static T Create<T>(IGeneratedStore? parent) where T : class
        {
            return (T)Create(typeof(T), parent);
        }

        private static IConfigStore Create(Type type, IGeneratedStore? parent)
        {
            return GetCreator(type)(parent);
        }

        private static (GeneratedStoreCreator ctor, Type type) GetCreatorAndGeneratedType(Type t)
        {
            return generatedCreators.GetOrAdd(t, MakeCreator);
        }

        internal static GeneratedStoreCreator GetCreator(Type t)
        {
            return GetCreatorAndGeneratedType(t).ctor;
        }

        internal static Type GetGeneratedType(Type t)
        {
            return GetCreatorAndGeneratedType(t).type;
        }

        internal static void DebugSaveAssembly(string file)
        {
            Assembly.Save(file);
        }

        private static void CreateAndInitializeConvertersFor(Type type, IEnumerable<SerializedMemberInfo> structure)
        {
            if (!TypeRequiredConverters.TryGetValue(type, out Dictionary<Type, FieldInfo>? converters))
            {
                TypeBuilder? converterFieldType = Module.DefineType($"{type.FullName}<Converters>",
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract |
                    TypeAttributes.AnsiClass); // a static class

                Type[]? uniqueConverterTypes = structure.Where(m => m.HasConverter)
                    .Select(m => m.Converter).NonNull().Distinct().ToArray();
                converters = new Dictionary<Type, FieldInfo>(uniqueConverterTypes.Length);

                foreach (Type? convType in uniqueConverterTypes)
                {
                    FieldBuilder? field = converterFieldType.DefineField($"<converter>_{convType}", convType,
                        FieldAttributes.FamORAssem | FieldAttributes.InitOnly | FieldAttributes.Static);
                    converters.Add(convType, field);
                }

                ConstructorBuilder? cctor = converterFieldType.DefineConstructor(MethodAttributes.Static,
                    CallingConventions.Standard, Type.EmptyTypes);
                {
                    ILGenerator? il = cctor.GetILGenerator();

                    foreach (KeyValuePair<Type, FieldInfo> kvp in converters)
                    {
                        ConstructorInfo? typeCtor = kvp.Key.GetConstructor(Type.EmptyTypes);
                        il.Emit(OpCodes.Newobj, typeCtor);
                        il.Emit(OpCodes.Stsfld, kvp.Value);
                    }

                    il.Emit(OpCodes.Ret);
                }

                TypeRequiredConverters.Add(type, converters);

                _ = converterFieldType.CreateType();
            }

            foreach (SerializedMemberInfo? member in structure)
            {
                if (!member.HasConverter)
                {
                    continue;
                }

                member.ConverterField = converters[member.Converter];
            }
        }
    }
}
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace IPA.Injector
{
    internal class VirtualizedModule : IDisposable
    {
        private readonly FileInfo file;

        private TypeReference inModreqRef;
        private ModuleDefinition module;

        private VirtualizedModule(string assemblyFile)
        {
            file = new FileInfo(assemblyFile);

            LoadModules();
        }

        public static VirtualizedModule Load(string engineFile)
        {
            return new VirtualizedModule(engineFile);
        }

        private void LoadModules()
        {
            module = ModuleDefinition.ReadModule(file.FullName,
                new ReaderParameters { ReadWrite = false, InMemory = true, ReadingMode = ReadingMode.Immediate });
        }

        public void Virtualize(AssemblyName selfName, Action beforeChangeCallback = null)
        {
            bool changed = false;
            bool virtualize = true;
            foreach (AssemblyNameReference r in module.AssemblyReferences)
            {
                if (r.Name == selfName.Name)
                {
                    virtualize = false;
                    if (r.Version != selfName.Version)
                    {
                        r.Version = selfName.Version;
                        changed = true;
                    }
                }
            }

            if (virtualize)
            {
                changed = true;
                module.AssemblyReferences.Add(new AssemblyNameReference(selfName.Name, selfName.Version));

                foreach (TypeDefinition type in module.Types)
                {
                    VirtualizeType(type);
                }
            }

            if (changed)
            {
                beforeChangeCallback?.Invoke();
                module.Write(file.FullName);
            }
        }
        // private TypeReference outModreqRef;

        private void VirtualizeType(TypeDefinition type)
        {
            if (type.IsSealed)
            {
                // Unseal
                type.IsSealed = false;
            }

            if (type.IsNestedPrivate)
            {
                type.IsNestedPrivate = false;
                type.IsNestedPublic = true;
            }

            if (type.IsInterface)
            {
                return;
            }

            if (type.IsAbstract)
            {
                return;
            }

            // These two don't seem to work.
            if (type.Name == "SceneControl" || type.Name == "ConfigUI")
            {
                return;
            }

            // Take care of sub types
            foreach (TypeDefinition subType in type.NestedTypes)
            {
                VirtualizeType(subType);
            }

            foreach (MethodDefinition method in type.Methods)
            {
                if (method.IsManaged
                    && method.IsIL
                    && !method.IsStatic
                    && (!method.IsVirtual || method.IsFinal)
                    && !method.IsAbstract
                    && !method.IsAddOn
                    && !method.IsConstructor
                    && !method.IsSpecialName
                    && !method.IsGenericInstance
                    && !method.HasOverrides)
                {
                    // fix In parameters to have the modreqs required by the compiler
                    foreach (ParameterDefinition param in method.Parameters)
                    {
                        if (param.IsIn)
                        {
                            inModreqRef ??= module.ImportReference(typeof(InAttribute));
                            param.ParameterType = AddModreqIfNotExist(param.ParameterType, inModreqRef);
                        }
                        // Breaks override methods if modreq is applied to `out` parameters
                        //if (param.IsOut)
                        //{
                        //    outModreqRef ??= module.ImportReference(typeof(System.Runtime.InteropServices.OutAttribute));
                        //    param.ParameterType = AddModreqIfNotExist(param.ParameterType, outModreqRef);
                        //}
                    }

                    method.IsVirtual = true;
                    method.IsFinal = false;
                    method.IsPublic = true;
                    method.IsPrivate = false;
                    method.IsNewSlot = true;
                    method.IsHideBySig = true;
                }
            }

            foreach (FieldDefinition field in type.Fields)
            {
                if (field.IsPrivate)
                {
                    field.IsFamily = true;
                }
            }
        }

        private TypeReference AddModreqIfNotExist(TypeReference type, TypeReference mod)
        {
            (TypeReference element, List<TypeReference> opt, List<TypeReference> req) = GetDecomposedModifiers(type);
            if (!req.Contains(mod))
            {
                req.Add(mod);
            }

            return BuildModifiedType(element, opt, req);
        }

        private (TypeReference Element, List<TypeReference> ModOpt, List<TypeReference> ModReq) GetDecomposedModifiers(
            TypeReference type)
        {
            List<TypeReference> opt = new();
            List<TypeReference> req = new();

            while (type is IModifierType modif)
            {
                if (type.IsOptionalModifier)
                {
                    opt.Add(modif.ModifierType);
                }

                if (type.IsRequiredModifier)
                {
                    req.Add(modif.ModifierType);
                }

                type = modif.ElementType;
            }

            return (type, opt, req);
        }

        private TypeReference BuildModifiedType(TypeReference type, IEnumerable<TypeReference> opt,
            IEnumerable<TypeReference> req)
        {
            foreach (TypeReference mod in req)
            {
                type = type.MakeRequiredModifierType(mod);
            }

            foreach (TypeReference mod in opt)
            {
                type = type.MakeOptionalModifierType(mod);
            }

            return type;
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    module.Dispose();
                }

                disposedValue = true;
            }
        }

        ~VirtualizedModule()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
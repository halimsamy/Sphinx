using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Pdb;
using Microsoft.Extensions.Logging;
using Sphinx.SymbolsObfuscation;

namespace Sphinx.Components
{
    internal class SymbolsObfuscationComponent : Component
    {
        private readonly ILogger<SymbolsObfuscationComponent> _logger;
        private readonly SymbolsObfuscationService _service;
        private readonly List<ICompatibilityLayer> _compatibilityLayers;

        public SymbolsObfuscationComponent(ILogger<SymbolsObfuscationComponent> logger)
        {
            this._logger = logger;
            this._service = new SymbolsObfuscationService();
            this._compatibilityLayers = new List<ICompatibilityLayer>();
            this.ResolveCompatibilityLayers();
        }

        #region Utility

        private void ResolveCompatibilityLayers()
        {
            foreach (var layerType in typeof(Program).Assembly.GetTypes()
                .Where(t => !t.IsInterface && !t.IsAbstract && typeof(ICompatibilityLayer).IsAssignableFrom(t)))
                this._compatibilityLayers.Add((ICompatibilityLayer) Activator.CreateInstance(layerType));
        }

        #endregion

        #region Phases

        [ComponentExecutionPoint(ExecutionPhase.Analyze)]
        private void Analyze()
        {
            foreach (var def in this.Context.Module.GetDefinitions())
            {
                if (def is IMemberDef member && member.IsVisibleOutside() && !this.RenamePublic)
                    this._service.SetCanRename(def, false);

                switch (def)
                {
                    case ModuleDef module:
                    {
                        this._service.SetCanRename(module, false);
                        break;
                    }
                    case TypeDef type:
                    {
                        if (type.IsRuntimeSpecialName || type.IsSpecialName || type.IsGlobalModuleType)
                            this._service.SetCanRename(type, false);

                        if (this.Force) return;

                        //if (type.InheritsFromCorlib("System.Attribute"))
                        //    _service.ReduceRenameMode(type, RenameMode.ASCII);

                        if (type.InheritsFrom("System.Configuration.SettingsBase"))
                            this._service.SetCanRename(type, false);
                        break;
                    }
                    case MethodDef method:
                    {
                        if (method.IsRuntimeSpecialName || method.IsSpecialName)
                            this._service.SetCanRename(method, false);

                        //method.IsExplicitlyImplementedInterfaceMember()
                        else if (method.IsInterfaceImplementation())
                            this._service.SetCanRename(method, false);

                        else if (this.Force)
                            continue;

                        else if (method.DeclaringType.IsComImport()
                                 && !method.HasAttribute("System.Runtime.InteropServices.DispIdAttribute"))
                            this._service.SetCanRename(method, false);

                        else if (method.DeclaringType.IsDelegate())
                            this._service.SetCanRename(method, false);

                        break;
                    }
                    case FieldDef field:
                    {
                        if (field.IsRuntimeSpecialName || field.IsSpecialName)
                            this._service.SetCanRename(field, false);

                        else if (this.Force)
                            continue;

                        else if (field.DeclaringType.IsSerializable && !field.IsNotSerialized)
                            this._service.SetCanRename(field, false);

                        else if (field.IsLiteral && field.DeclaringType.IsEnum && this.RenameEnum)
                            this._service.SetCanRename(field, false);

                        break;
                    }
                    case PropertyDef property:
                    {
                        if (property.IsRuntimeSpecialName || property.IsSpecialName)
                            this._service.SetCanRename(property, false);

                        else if (this.Force)
                            continue;

                        else if (property.DeclaringType.Implements("System.ComponentModel.INotifyPropertyChanged"))
                            this._service.SetCanRename(property, false);

                        else if (property.DeclaringType.Name.String.Contains("AnonymousType"))
                            this._service.SetCanRename(property, false);

                        break;
                    }
                    case EventDef evt:
                    {
                        if (evt.IsRuntimeSpecialName || evt.IsSpecialName)
                            this._service.SetCanRename(evt, false);

                        break;
                    }
                }

                this._compatibilityLayers.ForEach(layer => layer.Analyze(this.Context, def, this._service));
            }
        }

        [ComponentExecutionPoint(ExecutionPhase.Apply)]
        private void Apply()
        {
            var pdbDocs = new HashSet<string>();

            foreach (var def in this.Context.Module.GetDefinitions())
            {
                var canRename = this._service.CanRename(def);

                if (def is MethodDef method)
                {
                    if (this.RenameArgs)
                        foreach (var param in method.ParamDefs)
                            param.Name = null;

                    foreach (var param in method.GenericParameters)
                        param.Name = ((char) (param.Number + 1)).ToString();

                    if (this.RenamePdb && method.HasBody)
                    {
                        foreach (var instr in method.Body.Instructions)
                            if (instr.SequencePoint != null && !pdbDocs.Contains(instr.SequencePoint.Document.Url))
                            {
                                instr.SequencePoint.Document.Url =
                                    this._service.ObfuscateName(instr.SequencePoint.Document.Url);
                                pdbDocs.Add(instr.SequencePoint.Document.Url);
                            }

                        foreach (var local in method.Body.Variables
                            .Where(local => !string.IsNullOrEmpty(local.Name)))
                            local.Name = this._service.ObfuscateName(local.Name);

                        if (method.Body.HasPdbMethod)
                            method.Body.PdbMethod.Scope = new PdbScope();
                    }

                    if (canRename)
                        method.Name = this._service.ObfuscateName(method.Name);

                    continue;
                }

                if (!canRename)
                    continue;

                if (def is TypeDef type)
                {
                    if (this.Flatten)
                    {
                        type.Name = this._service.ObfuscateName(type.Name); // or type.FullName?
                        type.Namespace = "";
                    }
                    else
                    {
                        type.Name = this._service.ObfuscateName(type.Name);
                        type.Namespace = this._service.ObfuscateName(type.Namespace);
                    }

                    foreach (var param in type.GenericParameters)
                        param.Name = ((char) (param.Number + 1)).ToString();
                }
                else
                {
                    def.Name = this._service.ObfuscateName(def.Name);
                }

                this._compatibilityLayers.ForEach(layer => layer.Apply(this.Context, def, this._service));

                var references = this._service.GetReferences(def);
                var updatedReferences = -1;
                do
                {
                    var oldUpdatedCount = updatedReferences;
                    // This resolves the changed name references and counts how many were changed.
                    var updatedReferenceList = references
                        .Where(refer => refer.UpdateNameReference(this.Context, this._service)).ToArray();
                    updatedReferences = updatedReferenceList.Length;
                    if (updatedReferences == oldUpdatedCount)
                        throw new Exception("Infinite loop detected while resolving name references.");
                } while (updatedReferences > 0);
            }
        }

        [ComponentExecutionPoint(ExecutionPhase.Finishes)]
        private void Finishes()
        {
            /*
            foreach (var typeRef in this.Context.Module.GetTypeRefs())
            {
                this._logger.LogDebug(typeRef.ToString());
                typeRef.Name = this._service.GetObfuscatedName(typeRef.Name);
                typeRef.Namespace = this._service.GetObfuscatedName(typeRef.Namespace);
            }

            foreach (var member in this.Context.Module.GetMemberRefs())
                member.Name = this._service.GetObfuscatedName(member.Name);
            */
        }

        #endregion

        #region Params

        [ComponentParam("Args", true)] public bool RenameArgs;

        [ComponentParam("Enum", false)] public bool RenameEnum;

        [ComponentParam("Public", false)] public bool RenamePublic;

        [ComponentParam("Pdb", false)] public bool RenamePdb;

        [ComponentParam("Flatten", true)] public bool Flatten;

        [ComponentParam("Force", false)] public bool Force;

        #endregion

        #region Details

        public override string Id => "Rename";
        public override string Name => "Symbols Obfuscation (Renaming)";

        public override string Description =>
            "Obfuscates the symbols' name so the decompiled source code cannot be read/understand.";

        public override ComponentUsage Usage => ComponentUsage.Protecting;
        public override int Priority => int.MaxValue; // Should be the last to execute.

        #endregion
    }
}
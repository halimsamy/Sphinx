using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using Sphinx.SymbolsObfuscation.NameReferences;

namespace Sphinx.SymbolsObfuscation.CompatibilityLayers
{
    internal class InterReferenceCompatibilityLayer : ICompatibilityLayer
    {
        private void ProcessMemberRef(ICollection<ModuleDefMD> modules, ModuleDefMD module, IMemberRef @ref,
            SymbolsObfuscationService service)
        {
            var memberRef = @ref as MemberRef;
            if (@ref is MethodSpec spec)
                memberRef = spec.Method as MemberRef;

            if (memberRef == null) return;
            if (memberRef.DeclaringType.TryGetArraySig() != null) return;
            var declType = memberRef.DeclaringType.ResolveTypeDefThrow();
            if (declType.Module == module || !modules.Contains((ModuleDefMD) declType.Module)) return;
            var memberDef = (IMemberDef) declType.ResolveThrow(memberRef);
            service.AddReference(memberDef, new MemberRefReference(memberDef, memberRef));
        }

        public void Analyze(Context ctx, IDnlibDef def, SymbolsObfuscationService service)
        {
            if (!(def is ModuleDefMD module)) return;

            var modules = Context.Contexts.Select(c => c.Module).ToList();

            // MemberRef/MethodSpec
            var methods = module.GetTypes().SelectMany(type => type.Methods);
            foreach (var methodDef in methods)
            {
                foreach (var ov in methodDef.Overrides)
                {
                    this.ProcessMemberRef(modules, module, ov.MethodBody, service);
                    this.ProcessMemberRef(modules, module, ov.MethodDeclaration, service);
                }

                if (!methodDef.HasBody) continue;

                foreach (var instr in methodDef.Body.Instructions)
                    if (instr.Operand is MemberRef || instr.Operand is MethodSpec)
                        this.ProcessMemberRef(modules, module, (IMemberRef) instr.Operand, service);
            }

            // TypeRef
            var table = module.TablesStream.Get(Table.TypeRef);
            var len = table.Rows;
            for (uint i = 1; i <= len; i++)
            {
                var typeRef = module.ResolveTypeRef(i);
                var typeDef = typeRef.ResolveTypeDefThrow();
                if (typeDef.Module != module && modules.Contains((ModuleDefMD) typeDef.Module))
                    service.AddReference(typeDef, new TypeRefReference(typeDef, typeRef));
            }
        }

        public void Apply(Context ctx, IDnlibDef def, SymbolsObfuscationService service)
        {
        }
    }
}
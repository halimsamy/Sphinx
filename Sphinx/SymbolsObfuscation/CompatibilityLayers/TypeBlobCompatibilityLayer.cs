using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using Sphinx.SymbolsObfuscation.NameReferences;

namespace Sphinx.SymbolsObfuscation.CompatibilityLayers
{
    internal class TypeBlobCompatibilityLayer : ICompatibilityLayer
    {
        private void AnalyzeMemberRef(ICollection<ModuleDefMD> modules, MemberRef memberRef,
            SymbolsObfuscationService service)
        {
            var declType = memberRef.DeclaringType;
            if (!(declType is TypeSpec typeSpec) || typeSpec.TypeSig.IsArray || typeSpec.TypeSig.IsSZArray) return;

            var sig = typeSpec.TypeSig;
            while (sig.Next != null) sig = sig.Next;

            //Debug.Assert(sig is TypeDefOrRefSig || sig is GenericInstSig || sig is GenericSig);
            if (!(sig is GenericInstSig inst)) return;
            //Debug.Assert(!(inst.GenericType.TypeDefOrRef is TypeSpec));
            var openType = inst.GenericType.TypeDefOrRef.ResolveTypeDefThrow();
            if (!modules.Contains((ModuleDefMD) openType.Module) ||
                memberRef.IsArrayAccessors())
                return;

            IMemberDef memberDef;
            if (memberRef.IsFieldRef) memberDef = memberRef.ResolveFieldThrow();
            else if (memberRef.IsMethodRef) memberDef = memberRef.ResolveMethodThrow();
            else throw new UnreachableException();

            service.AddReference(memberDef, new MemberRefReference(memberDef, memberRef));
        }

        public void Analyze(Context ctx, IDnlibDef def, SymbolsObfuscationService service)
        {
            if (!(def is ModuleDefMD module)) return;

            var modules = Context.Contexts.Select(c => c.Module).ToList();

            // MemberRef
            var table = module.TablesStream.Get(Table.Method);
            var methods = module.GetTypes().SelectMany(type => type.Methods);
            foreach (var method in methods)
            {
                foreach (var methodImpl in method.Overrides)
                {
                    if (methodImpl.MethodBody is MemberRef memberRefBody)
                        this.AnalyzeMemberRef(modules, memberRefBody, service);
                    if (methodImpl.MethodDeclaration is MemberRef memberRefDecl)
                        this.AnalyzeMemberRef(modules, memberRefDecl, service);
                }

                if (!method.HasBody) continue;

                foreach (var instr in method.Body.Instructions)
                    switch (instr.Operand)
                    {
                        case MemberRef memberRef:
                            this.AnalyzeMemberRef(modules, memberRef, service);
                            break;
                        case MethodSpec spec:
                        {
                            if (spec.Method is MemberRef memberRef)
                                this.AnalyzeMemberRef(modules, memberRef, service);
                            break;
                        }
                    }
            }
        }

        public void Apply(Context ctx, IDnlibDef def, SymbolsObfuscationService service)
        {
        }
    }
}
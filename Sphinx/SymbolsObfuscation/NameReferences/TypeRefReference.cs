using dnlib.DotNet;

namespace Sphinx.SymbolsObfuscation.NameReferences
{
    internal class TypeRefReference : INameReference<TypeDef>
    {
        private readonly TypeDef _typeDef;
        private readonly TypeRef _typeRef;

        public TypeRefReference(TypeDef typeDef, TypeRef typeRef)
        {
            this._typeDef = typeDef;
            this._typeRef = typeRef;
        }

        public bool UpdateNameReference(Context ctx, SymbolsObfuscationService service)
        {
            if (UTF8String.Equals(this._typeRef.Namespace, this._typeDef.Namespace) &&
                UTF8String.Equals(this._typeRef.Name, this._typeDef.Name)) return false;

            this._typeRef.Namespace = this._typeDef.Namespace;
            this._typeRef.Name = this._typeDef.Name;
            return true;
        }
    }
}
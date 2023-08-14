using dnlib.DotNet;

namespace Sphinx.SymbolsObfuscation.NameReferences
{
    internal class MemberRefReference : INameReference<IMemberDef>
    {
        private readonly IMemberDef _memberDef;
        private readonly MemberRef _memberRef;

        public MemberRefReference(IMemberDef memberDef, MemberRef memberRef)
        {
            this._memberDef = memberDef;
            this._memberRef = memberRef;
        }

        public bool UpdateNameReference(Context ctx, SymbolsObfuscationService service)
        {
            if (UTF8String.Equals(this._memberRef.Name, this._memberDef.Name)) return false;
            this._memberRef.Name = this._memberDef.Name;
            return true;
        }
    }
}
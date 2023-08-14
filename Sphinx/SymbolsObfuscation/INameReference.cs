namespace Sphinx.SymbolsObfuscation
{
    internal interface INameReference
    {
        bool UpdateNameReference(Context ctx, SymbolsObfuscationService service);
    }

    internal interface INameReference<out T> : INameReference
    {
    }
}
using dnlib.DotNet;

namespace Sphinx.SymbolsObfuscation
{
    /// <summary>
    ///     A Compatibility Layer for <see cref="Components.SymbolsObfuscationComponent" />
    /// </summary>
    internal interface ICompatibilityLayer
    {
        void Analyze(Context ctx, IDnlibDef def, SymbolsObfuscationService service);
        void Apply(Context ctx, IDnlibDef def, SymbolsObfuscationService service);
    }
}
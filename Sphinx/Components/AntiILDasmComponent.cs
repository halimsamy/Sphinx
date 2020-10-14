using System.Linq;
using dnlib.DotNet;
using Microsoft.Extensions.Logging;

namespace Sphinx.Components
{
    // ReSharper disable once InconsistentNaming
    internal class AntiILDasmComponent : Component
    {
        private readonly ILogger<AntiILDasmComponent> _logger;

        public AntiILDasmComponent(ILogger<AntiILDasmComponent> logger)
        {
            this._logger = logger;
        }

        public override string Id => "AntiILDasm";
        public override string Name => "Anti-ILDasm";

        public override string Description =>
            "Marks the modules with a attribute that discourage ILDasm from disassembling it.";

        public override ComponentUsage Usage => ComponentUsage.Protecting;
        public override int Priority => 0;

        public override void Execute(Context ctx, ExecutionPhase phase)
        {
            if (phase != ExecutionPhase.Apply) return;

            var attrRef =
                ctx.Module.CorLibTypes
                    .GetTypeRef("System.Runtime.CompilerServices", "SuppressIldasmAttribute");

            var ctorRef = new MemberRefUser(ctx.Module, ".ctor",
                MethodSig.CreateInstance(ctx.Module.CorLibTypes.Void), attrRef);

            var attr = new CustomAttribute(ctorRef);
            if (ctx.Module.CustomAttributes.All(a => a.ToString() != attr.ToString()))
                ctx.Module.CustomAttributes.Add(attr);
        }
    }
}
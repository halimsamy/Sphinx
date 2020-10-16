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


        [ComponentExecutionPoint(ExecutionPhase.Apply)]
        private void Apply()
        {
            var attrRef = this.Context.Module.CorLibTypes
                .GetTypeRef("System.Runtime.CompilerServices", "SuppressIldasmAttribute");

            var ctorRef = new MemberRefUser(this.Context.Module, ".ctor",
                MethodSig.CreateInstance(this.Context.Module.CorLibTypes.Void), attrRef);

            var attr = new CustomAttribute(ctorRef);
            if (this.Context.Module.CustomAttributes.All(a => a.ToString() != attr.ToString()))
                this.Context.Module.CustomAttributes.Add(attr);
        }

        #region Details

        public override string Id => "AntiILDasm";
        public override string Name => "Anti-ILDasm";

        public override string Description =>
            "Marks the modules with a attribute that discourage ILDasm from disassembling it.";

        public override ComponentUsage Usage => ComponentUsage.Protecting;
        public override int Priority => 0;

        #endregion
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;

namespace Sphinx.Components.ControlFlow
{
    internal abstract class BlockBase
    {
        protected BlockBase(BlockType type)
        {
            this.Type = type;
        }

        public ScopeBlock Parent { get; private set; }

        public BlockType Type { get; }
        public abstract void ToBody(CilBody body);
    }

    internal enum BlockType
    {
        Normal,
        Try,
        Handler,
        Finally,
        Filter,
        Fault
    }

    internal class ScopeBlock : BlockBase
    {
        public ScopeBlock(BlockType type, ExceptionHandler handler)
            : base(type)
        {
            this.Handler = handler;
            this.Children = new List<BlockBase>();
        }

        public ExceptionHandler Handler { get; }

        public List<BlockBase> Children { get; set; }

        public override string ToString()
        {
            var ret = new StringBuilder();
            switch (this.Type)
            {
                case BlockType.Try:
                    ret.Append("try ");
                    break;
                case BlockType.Handler:
                    ret.Append("handler ");
                    break;
                case BlockType.Finally:
                    ret.Append("finally ");
                    break;
                case BlockType.Fault:
                    ret.Append("fault ");
                    break;
            }

            ret.AppendLine("{");
            foreach (var child in this.Children)
                ret.Append(child);
            ret.AppendLine("}");
            return ret.ToString();
        }

        public override void ToBody(CilBody body)
        {
            if (this.Type != BlockType.Normal)
            {
                if (this.Type == BlockType.Try)
                {
                    this.Handler.TryStart = this.GetFirstInstr();
                    this.Handler.TryEnd = this.GetLastInstr();
                }
                else if (this.Type == BlockType.Filter)
                {
                    this.Handler.FilterStart = this.GetFirstInstr();
                }
                else
                {
                    this.Handler.HandlerStart = this.GetFirstInstr();
                    this.Handler.HandlerEnd = this.GetLastInstr();
                }
            }

            foreach (var block in this.Children)
                block.ToBody(body);
        }

        public Instruction GetFirstInstr()
        {
            var firstBlock = this.Children.First();
            return firstBlock is ScopeBlock block
                ? block.GetFirstInstr()
                : ((InstrBlock) firstBlock).Instructions.First();
        }

        public Instruction GetLastInstr()
        {
            var firstBlock = this.Children.Last();
            return firstBlock is ScopeBlock block
                ? block.GetLastInstr()
                : ((InstrBlock) firstBlock).Instructions.Last();
        }
    }

    internal class InstrBlock : BlockBase
    {
        public InstrBlock()
            : base(BlockType.Normal)
        {
            this.Instructions = new List<Instruction>();
        }

        public List<Instruction> Instructions { get; set; }

        public override string ToString()
        {
            var ret = new StringBuilder();
            foreach (var instr in this.Instructions)
                ret.AppendLine(instr.ToString());
            return ret.ToString();
        }

        public override void ToBody(CilBody body)
        {
            foreach (var instr in this.Instructions)
                body.Instructions.Add(instr);
        }
    }
}
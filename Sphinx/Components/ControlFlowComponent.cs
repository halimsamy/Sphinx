using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Pdb;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.Logging;
using Sphinx.Components.ControlFlow;

namespace Sphinx.Components
{
    internal class ControlFlowComponent : Component
    {
        private readonly ILogger<ControlFlowComponent> _logger;

        public ControlFlowComponent(ILogger<ControlFlowComponent> logger)
        {
            this._logger = logger;
        }

        public override string Id => "ControlFlow";
        public override string Name => "Control Flow";

        public override string Description =>
            "Mangles the code in the methods so that decompilers cannot decompile the methods.";

        public override ComponentPreset Preset => ComponentPreset.Normal;
        public override ComponentUsage Usage => ComponentUsage.Protecting;
        public override int Priority => 0;

        protected static IEnumerable<InstrBlock> GetAllBlocks(ScopeBlock scope)
        {
            foreach (var child in scope.Children)
                if (child is InstrBlock instrBlock)
                    yield return instrBlock;
                else
                    foreach (var block in GetAllBlocks((ScopeBlock) child))
                        yield return block;
        }

        public override void Analyze(Context ctx)
        {
            // ignored
        }

        public override void Execute(Context ctx)
        {
            foreach (var type in ctx.Module.GetTypes())
            foreach (var method in type.Methods)
            {
                if (!MaxStackCalculator.GetMaxStack(method.Body.Instructions, method.Body.ExceptionHandlers,
                    out var maxStack)) throw new Exception($"Failed to calculate MaxStack, Method: '{method}'.");
                method.Body.MaxStack = (ushort) maxStack;
                var root = BlockParser.ParseBody(method.Body);

                this.Mangle(method.Body, root, method, ctx);

                method.Body.Instructions.Clear();
                root.ToBody(method.Body);
                if (method.Body.PdbMethod != null)
                    method.Body.PdbMethod = new PdbMethod
                    {
                        Scope = new PdbScope
                        {
                            Start = method.Body.Instructions.First(),
                            End = method.Body.Instructions.Last()
                        }
                    };
                foreach (var eh in method.Body.ExceptionHandlers)
                {
                    var index = method.Body.Instructions.IndexOf(eh.TryEnd) + 1;
                    eh.TryEnd = index < method.Body.Instructions.Count ? method.Body.Instructions[index] : null;
                    index = method.Body.Instructions.IndexOf(eh.HandlerEnd) + 1;
                    eh.HandlerEnd = index < method.Body.Instructions.Count ? method.Body.Instructions[index] : null;
                }

                method.Body.KeepOldMaxStack = true;

                method.Body.SimplifyBranches();
            }
        }

        public override void Finalize(Context ctx)
        {
            // ignored
        }

        public void Mangle(CilBody body, ScopeBlock root, MethodDef method, Context ctx)
        {
            body.MaxStack++;
            foreach (var block in GetAllBlocks(root))
            {
                var fragments = this.SpiltFragments(block, ctx);
                if (fragments.Count < 4) continue;

                var current = fragments.First;
                while (current?.Next != null)
                {
                    var newFragment = new List<Instruction>(current.Value);
                    this.AddJump(newFragment, current.Next.Value[0], method, ctx);
                    this.AddJunk(newFragment, method, ctx);
                    current.Value = newFragment.ToArray();
                    current = current.Next;
                }

                var first = fragments.First?.Value;
                fragments.RemoveFirst();
                var last = fragments.Last?.Value;
                fragments.RemoveLast();

                var newFragments = fragments.ToList();
                Utility.Shuffle(newFragments);

                block.Instructions = first?
                    .Concat(newFragments.SelectMany(fragment => fragment))
                    .Concat(last).ToList();
            }
        }

        private LinkedList<Instruction[]> SpiltFragments(InstrBlock block, Context ctx)
        {
            var fragments = new LinkedList<Instruction[]>();
            var currentFragment = new List<Instruction>();

            var skipCount = -1;
            for (var i = 0; i < block.Instructions.Count; i++)
            {
                if (skipCount != -1)
                {
                    if (skipCount > 0)
                    {
                        currentFragment.Add(block.Instructions[i]);
                        skipCount--;
                        continue;
                    }

                    fragments.AddLast(currentFragment.ToArray());
                    currentFragment.Clear();

                    skipCount = -1;
                }

                if (block.Instructions[i].OpCode.OpCodeType == OpCodeType.Prefix)
                {
                    skipCount = 1;
                    currentFragment.Add(block.Instructions[i]);
                }

                if (i + 2 < block.Instructions.Count &&
                    block.Instructions[i + 0].OpCode.Code == Code.Dup &&
                    block.Instructions[i + 1].OpCode.Code == Code.Ldvirtftn &&
                    block.Instructions[i + 2].OpCode.Code == Code.Newobj)
                {
                    skipCount = 2;
                    currentFragment.Add(block.Instructions[i]);
                }

                if (i + 4 < block.Instructions.Count &&
                    block.Instructions[i + 0].OpCode.Code == Code.Ldc_I4 &&
                    block.Instructions[i + 1].OpCode.Code == Code.Newarr &&
                    block.Instructions[i + 2].OpCode.Code == Code.Dup &&
                    block.Instructions[i + 3].OpCode.Code == Code.Ldtoken &&
                    block.Instructions[i + 4].OpCode.Code == Code.Call) // Array initializer
                {
                    skipCount = 4;
                    currentFragment.Add(block.Instructions[i]);
                }

                if (i + 1 < block.Instructions.Count &&
                    block.Instructions[i + 0].OpCode.Code == Code.Ldftn &&
                    block.Instructions[i + 1].OpCode.Code == Code.Newobj)
                {
                    skipCount = 1;
                    currentFragment.Add(block.Instructions[i]);
                }

                currentFragment.Add(block.Instructions[i]);

                if (ctx.GetOptionValue($"{this.Id}:Intensity", 70) > RandomNumberGenerator.GetInt32(0, 101))
                {
                    fragments.AddLast(currentFragment.ToArray());
                    currentFragment.Clear();
                }
            }

            if (currentFragment.Count > 0)
                fragments.AddLast(currentFragment.ToArray());

            return fragments;
        }

        public void AddJump(IList<Instruction> instrs, Instruction target, MethodDef method, Context ctx)
        {
            if (!method.Module.IsClr40 && ctx.GetOptionValue($"{this.Id}:Junk", false) &&
                !method.DeclaringType.HasGenericParameters && !method.HasGenericParameters &&
                (instrs[0].OpCode.FlowControl == FlowControl.Call || instrs[0].OpCode.FlowControl == FlowControl.Next))
                switch (RandomNumberGenerator.GetInt32(0, 3))
                {
                    case 0:
                        instrs.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        instrs.Add(Instruction.Create(OpCodes.Brtrue, instrs[0]));
                        break;

                    case 1:
                        instrs.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                        instrs.Add(Instruction.Create(OpCodes.Brfalse, instrs[0]));
                        break;

                    case 2: // Take that, de4dot + ILSpy :)
                        var addDefOk = false;
                        if (RandomNumberGenerator.GetInt32(0, 1) == 1)
                        {
                            var randomType =
                                method.Module.Types[RandomNumberGenerator.GetInt32(method.Module.Types.Count)];

                            if (randomType.HasMethods)
                            {
                                instrs.Add(Instruction.Create(OpCodes.Ldtoken,
                                    randomType.Methods[RandomNumberGenerator.GetInt32(randomType.Methods.Count)]));
                                instrs.Add(Instruction.Create(OpCodes.Box,
                                    method.Module.CorLibTypes.GetTypeRef("System", "RuntimeMethodHandle")));
                                addDefOk = true;
                            }
                        }

                        if (!addDefOk)
                        {
                            instrs.Add(Instruction.Create(OpCodes.Ldc_I4, RandomNumberGenerator.GetInt32(0, 2)));
                            instrs.Add(Instruction.Create(OpCodes.Box, method.Module.CorLibTypes.Int32.TypeDefOrRef));
                        }

                        var pop = Instruction.Create(OpCodes.Pop);
                        instrs.Add(Instruction.Create(OpCodes.Brfalse, instrs[0]));
                        instrs.Add(Instruction.Create(OpCodes.Ldc_I4, RandomNumberGenerator.GetInt32(0, 2)));
                        instrs.Add(pop);
                        break;
                }

            instrs.Add(Instruction.Create(OpCodes.Br, target));
        }

        public void AddJunk(IList<Instruction> instrs, MethodDef method, Context ctx)
        {
            if (method.Module.IsClr40 || !ctx.GetOptionValue($"{this.Id}:Junk", false))
                return;

            switch (RandomNumberGenerator.GetInt32(0, 6))
            {
                case 0:
                    instrs.Add(Instruction.Create(OpCodes.Pop));
                    break;
                case 1:
                    instrs.Add(Instruction.Create(OpCodes.Dup));
                    break;
                case 2:
                    instrs.Add(Instruction.Create(OpCodes.Throw));
                    break;
                case 3:
                    instrs.Add(Instruction.Create(OpCodes.Ldarg, new Parameter(0xff)));
                    break;
                case 4:
                    instrs.Add(Instruction.Create(OpCodes.Ldloc, new Local(null, null, 0xff)));
                    break;
                case 5:
                    instrs.Add(Instruction.Create(OpCodes.Ldtoken, method));
                    break;
            }
        }
    }
}
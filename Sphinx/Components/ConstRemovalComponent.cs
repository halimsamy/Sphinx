using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.Logging;

namespace Sphinx.Components
{
    internal class ConstRemovalComponent : Component
    {
        private readonly Dictionary<TypeDef, MethodDef> _decoders;
        private readonly Dictionary<TypeDef, byte[]> _keys;
        private readonly Dictionary<Instruction, MethodDef> _loads;

        private readonly ILogger<ConstRemovalComponent> _logger;

        public ConstRemovalComponent(ILogger<ConstRemovalComponent> logger)
        {
            this._logger = logger;
            this._loads = new Dictionary<Instruction, MethodDef>();
            this._decoders = new Dictionary<TypeDef, MethodDef>();
            this._keys = new Dictionary<TypeDef, byte[]>();
        }


        public override void Execute(Context ctx, ExecutionPhase phase)
        {
            if (phase == ExecutionPhase.Analyze) this.Analyze(ctx);
            else if (phase == ExecutionPhase.Apply) this.Apply(ctx);
        }

        private Tuple<MethodDef, byte[]> CreateDecodeMethod(ModuleDef mod, TypeDef type)
        {
            // Generate the Key.
            var xorKey = new byte[16];
            RandomNumberGenerator.Fill(xorKey.AsSpan());

            // Write the go to a new embedded resource.
            var resName = Extensions.RandomString();
            mod.Resources.Add(new EmbeddedResource(resName, xorKey));

            // Define a new field to hold the key!
            var keyField = new FieldDefUser(Extensions.RandomString(),
                new FieldSig(new SZArraySig(mod.CorLibTypes.Byte)),
                FieldAttributes.Private | FieldAttributes.Static);
            type.Fields.Add(keyField);

            #region References

            var objectRef = mod.CorLibTypes.GetTypeRef("System", "Object");
            var equalsRef = new MemberRefUser(mod, "Equals",
                MethodSig.CreateInstance(mod.CorLibTypes.Boolean, mod.CorLibTypes.Object), objectRef);

            var streamRef = mod.CorLibTypes.GetTypeRef("System.IO", "Stream");
            var readRef = new MemberRefUser(mod, "Read",
                MethodSig.CreateInstance(mod.CorLibTypes.Int32, new SZArraySig(mod.CorLibTypes.Byte),
                    mod.CorLibTypes.Int32, mod.CorLibTypes.Int32), streamRef);

            var assemblyRef = mod.CorLibTypes.GetTypeRef("System.Reflection", "Assembly");
            var getExecutingAssemblyRef = new MemberRefUser(mod, "GetExecutingAssembly",
                MethodSig.CreateStatic(assemblyRef.ToTypeSig()),
                assemblyRef);
            var getCallingAssemblyRef = new MemberRefUser(mod, "GetCallingAssembly",
                MethodSig.CreateStatic(assemblyRef.ToTypeSig()),
                assemblyRef);
            var getManifestResourceStreamRef = new MemberRefUser(mod, "GetManifestResourceStream",
                MethodSig.CreateInstance(streamRef.ToTypeSig(), mod.CorLibTypes.String),
                assemblyRef);

            var stringRef = mod.CorLibTypes.GetTypeRef("System", "String");
            var stringCtorRef = new MemberRefUser(mod, ".ctor",
                MethodSig.CreateInstance(mod.CorLibTypes.Void, new SZArraySig(mod.CorLibTypes.Char)),
                stringRef);
            var getLengthRef = new MemberRefUser(mod, "get_Length",
                MethodSig.CreateInstance(mod.CorLibTypes.Int32),
                stringRef);
            var toCharArrayRef = new MemberRefUser(mod, "ToCharArray",
                MethodSig.CreateInstance(new SZArraySig(mod.CorLibTypes.Char)),
                stringRef);

            #endregion

            #region Method Sig

            // Create a private static method with a random name that returns string and takes int32 as arg.
            var method = new MethodDefUser(Extensions.RandomString(),
                MethodSig.CreateStatic(mod.CorLibTypes.String, mod.CorLibTypes.String, mod.CorLibTypes.Int32),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig |
                MethodAttributes.ReuseSlot);
            // Define the params
            method.ParamDefs.Add(new ParamDefUser(Extensions.RandomString(), 1));
            method.ParamDefs.Add(new ParamDefUser(Extensions.RandomString(), 2));

            #endregion

            // Define the method's body.
            // The implementation of the body is the same as Extensions.XOR(s, n, key)
            // the only diff is that it loads the key from the resources.
            var body = new CilBody();

            #region Local Variables

            // Stream stream;
            body.Variables.Add(new Local(streamRef.ToTypeSig(), Extensions.RandomString()));
            // int num;
            body.Variables.Add(new Local(mod.CorLibTypes.Int32, Extensions.RandomString()));
            // char[] array;
            body.Variables.Add(new Local(new SZArraySig(mod.CorLibTypes.Char), Extensions.RandomString()));

            #endregion

            #region Jump Instructions

            // Instructions used for jumping!
            var loadEmptyStr = OpCodes.Ldstr.ToInstruction("");
            var start = OpCodes.Ldarg_0.ToInstruction();
            var checkLoop = OpCodes.Ldloc_1.ToInstruction();
            var loop = OpCodes.Ldloc_2.ToInstruction();
            var initKeyField = OpCodes.Ldc_I4.ToInstruction(16);

            #endregion

            #region Check Assembly

            // if (!Assembly.GetExecutingAssembly().Equals(Assembly.GetCallingAssembly())) goto returnNothing;
            body.Instructions.Add(OpCodes.Call.ToInstruction(getExecutingAssemblyRef));
            body.Instructions.Add(OpCodes.Call.ToInstruction(getCallingAssemblyRef));
            body.Instructions.Add(OpCodes.Callvirt.ToInstruction(equalsRef));
            body.Instructions.Add(Instruction.Create(OpCodes.Brfalse, loadEmptyStr));

            #endregion

            #region Check Key Field

            // if (keyField != null) goto start;
            body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(keyField));
            body.Instructions.Add(Instruction.Create(OpCodes.Brtrue, start));

            #endregion

            #region Load/Init the Key

            // Stream stream = Assembly.GetCallingAssembly().GetManifestResourceStream(resName);
            // NOTE: Use GetCallingAssembly() not GetExecutingAssembly()
            // This helps tricking de4dot (--strtyp delegate --strtok 0x0000)
            body.Instructions.Add(OpCodes.Call.ToInstruction(getCallingAssemblyRef));
            body.Instructions.Add(OpCodes.Ldstr.ToInstruction(resName));
            body.Instructions.Add(OpCodes.Callvirt.ToInstruction(getManifestResourceStreamRef));
            body.Instructions.Add(OpCodes.Stloc_0.ToInstruction());
            // if (stream != null) goto initKeyField;
            body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
            body.Instructions.Add(Instruction.Create(OpCodes.Brtrue, initKeyField));
            // else return "";
            body.Instructions.Add(OpCodes.Ldstr.ToInstruction(""));
            body.Instructions.Add(OpCodes.Ret.ToInstruction());
            // initKeyField: 
            // keyField = new byte[16];
            body.Instructions.Add(initKeyField);
            body.Instructions.Add(OpCodes.Newarr.ToInstruction(mod.CorLibTypes.Byte));
            body.Instructions.Add(OpCodes.Stsfld.ToInstruction(keyField));
            // stream.Read(keyField, 0, 16);
            body.Instructions.Add(OpCodes.Ldloc_0.ToInstruction());
            body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(keyField));
            body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(16));
            body.Instructions.Add(OpCodes.Callvirt.ToInstruction(readRef));
            body.Instructions.Add(OpCodes.Pop.ToInstruction());

            #endregion

            #region Start

            // start:
            // num = strArg.Length;
            body.Instructions.Add(start);
            body.Instructions.Add(OpCodes.Callvirt.ToInstruction(getLengthRef));
            body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
            // array = strArg.ToCharArray();
            body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            body.Instructions.Add(OpCodes.Callvirt.ToInstruction(toCharArrayRef));
            body.Instructions.Add(OpCodes.Stloc_2.ToInstruction());
            // goto checkLoop;
            body.Instructions.Add(Instruction.Create(OpCodes.Br, checkLoop));

            #endregion

            #region Loop

            // loop:
            // array[num] = (char)((int)array[num] ^ ((int)this.c[f & 15] | f));
            body.Instructions.Add(loop);
            body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
            body.Instructions.Add(OpCodes.Ldloc_2.ToInstruction());
            body.Instructions.Add(OpCodes.Ldloc_1.ToInstruction());
            body.Instructions.Add(OpCodes.Ldelem_U2.ToInstruction());
            body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(keyField));
            body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(15));
            body.Instructions.Add(OpCodes.And.ToInstruction());
            body.Instructions.Add(OpCodes.Ldelem_U1.ToInstruction());
            body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            body.Instructions.Add(OpCodes.Or.ToInstruction());
            body.Instructions.Add(OpCodes.Xor.ToInstruction());
            body.Instructions.Add(OpCodes.Conv_U2.ToInstruction());
            body.Instructions.Add(OpCodes.Stelem_I2.ToInstruction());

            #endregion

            #region Loop Checker

            // checkLoop:
            // num--;
            body.Instructions.Add(checkLoop);
            body.Instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
            body.Instructions.Add(OpCodes.Sub.ToInstruction());
            body.Instructions.Add(OpCodes.Dup.ToInstruction());
            body.Instructions.Add(OpCodes.Stloc_1.ToInstruction());
            // if (num == 0) goto loop;
            body.Instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
            body.Instructions.Add(Instruction.Create(OpCodes.Bge, loop));

            #endregion

            #region Return the Value

            // return new string(array);
            body.Instructions.Add(OpCodes.Ldloc_2.ToInstruction());
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(stringCtorRef));
            body.Instructions.Add(OpCodes.Ret.ToInstruction());

            // returnNothing:
            // return "";
            body.Instructions.Add(loadEmptyStr);
            body.Instructions.Add(OpCodes.Ret.ToInstruction());

            #endregion

            body.OptimizeBranches();
            method.Body = body;
            type.Methods.Add(method);

            return Tuple.Create((MethodDef) method, xorKey);
        }

        #region Phases

        private void Analyze(Context ctx)
        {
            foreach (var type in ctx.Module.GetTypes())
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                foreach (var instruction in method.Body.Instructions)
                    if (instruction.OpCode == OpCodes.Ldstr)
                    {
                        var operand = (string) instruction.Operand;
                        if (string.IsNullOrEmpty(operand)) continue;
                        this._loads.Add(instruction, method);
                    }
            }
        }

        private void Apply(Context ctx)
        {
            foreach (var (instruction, method) in this._loads.Where(p => p.Value.DeclaringType.Module == ctx.Module))
            {
                // shortcuts!
                var type = method.DeclaringType;
                var body = method.Body;

                var key = this._keys.GetValueOrDefault(type);
                // Get the Decoding Method or create a new one if it doesn't exists.
                if (!this._decoders.TryGetValue(type, out var decoderMethod))
                {
                    var tuple = this.CreateDecodeMethod(ctx.Module, type);
                    decoderMethod = tuple.Item1;
                    key = tuple.Item2;
                    this._decoders.Add(type, decoderMethod);
                    this._keys.Add(type, key);
                }

                // Get the index where we would start hooking at.
                var instructionIndex = body.Instructions.IndexOf(instruction);

                var num = RandomNumberGenerator.GetInt32(int.MaxValue);

                if (instruction.OpCode == OpCodes.Ldstr)
                {
                    instruction.Operand = ((string) instruction.Operand).XOR(num, key);

                    body.Instructions.Insert(instructionIndex + 1, OpCodes.Ldc_I4.ToInstruction(num));

                    // Insert the call to the decoder method right after we load the param.
                    body.Instructions.Insert(instructionIndex + 2, OpCodes.Call.ToInstruction(decoderMethod));
                }
            }
        }

        #endregion

        #region Details

        public override string Id => "ConstRemoval";
        public override string Name => "Constants Removal";
        public override string Description => "Encodes constants in the code making it harder to read/understand.";
        public override ComponentUsage Usage => ComponentUsage.Protecting;
        public override int Priority => 1; // [EX] Should run before the Control Flow, and after the Ref Proxy.

        #endregion
    }
}
using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Sphinx
{
    /// <summary>
    ///     Resolves generic arguments
    /// </summary>
    public ref struct GenericArgumentResolver
    {
        private GenericArguments _genericArguments;
        private RecursionCounter _recursionCounter;

        /// <summary>
        ///     Resolves the type signature with the specified generic arguments.
        /// </summary>
        /// <param name="typeSig">The type signature.</param>
        /// <param name="typeGenArgs">The type generic arguments.</param>
        /// <returns>Resolved type signature.</returns>
        /// <exception cref="System.ArgumentException">No generic arguments to resolve.</exception>
        public static TypeSig Resolve(TypeSig typeSig, IList<TypeSig> typeGenArgs)
        {
            if (typeGenArgs == null) throw new ArgumentException("No generic arguments to resolve.");

            var resolver = new GenericArgumentResolver
            {
                _genericArguments = new GenericArguments(),
                _recursionCounter = new RecursionCounter()
            };
            resolver._genericArguments.PushTypeArgs(typeGenArgs);

            return resolver.ResolveGenericArgs(typeSig);
        }

        /// <summary>
        ///     Resolves the method signature with the specified generic arguments.
        /// </summary>
        /// <param name="methodSig">The method signature.</param>
        /// <param name="typeGenArgs">The type generic arguments.</param>
        /// <returns>Resolved method signature.</returns>
        /// <exception cref="System.ArgumentException">No generic arguments to resolve.</exception>
        public static MethodSig Resolve(MethodSig methodSig, IList<TypeSig> typeGenArgs)
        {
            if (typeGenArgs == null)
                throw new ArgumentException("No generic arguments to resolve.");

            var resolver = new GenericArgumentResolver
            {
                _genericArguments = new GenericArguments(),
                _recursionCounter = new RecursionCounter()
            };
            resolver._genericArguments.PushTypeArgs(typeGenArgs);

            return resolver.ResolveGenericArgs(methodSig);
        }

        private bool ReplaceGenericArg(ref TypeSig typeSig)
        {
            var newTypeSig = this._genericArguments.Resolve(typeSig);
            if (newTypeSig == typeSig) return false;

            typeSig = newTypeSig;
            return true;
        }

        private MethodSig ResolveGenericArgs(MethodSig sig)
        {
            if (sig == null)
                return null;
            if (!this._recursionCounter.Increment())
                return null;

            var result = this.ResolveGenericArgs(new MethodSig(sig.GetCallingConvention()), sig);

            this._recursionCounter.Decrement();
            return result;
        }

        private MethodSig ResolveGenericArgs(MethodSig sig, MethodSig old)
        {
            sig.RetType = this.ResolveGenericArgs(old.RetType);
            foreach (var p in old.Params)
                sig.Params.Add(this.ResolveGenericArgs(p));
            sig.GenParamCount = old.GenParamCount;
            if (sig.ParamsAfterSentinel != null)
                foreach (var p in old.ParamsAfterSentinel)
                    sig.ParamsAfterSentinel.Add(this.ResolveGenericArgs(p));
            return sig;
        }

        private TypeSig ResolveGenericArgs(TypeSig typeSig)
        {
            if (!this._recursionCounter.Increment())
                return null;

            if (this.ReplaceGenericArg(ref typeSig))
            {
                this._recursionCounter.Decrement();
                return typeSig;
            }

            TypeSig result;
            switch (typeSig.ElementType)
            {
                case ElementType.Ptr:
                    result = new PtrSig(this.ResolveGenericArgs(typeSig.Next));
                    break;
                case ElementType.ByRef:
                    result = new ByRefSig(this.ResolveGenericArgs(typeSig.Next));
                    break;
                case ElementType.Var:
                    result = new GenericVar(((GenericVar) typeSig).Number);
                    break;
                case ElementType.ValueArray:
                    result = new ValueArraySig(this.ResolveGenericArgs(typeSig.Next), ((ValueArraySig) typeSig).Size);
                    break;
                case ElementType.SZArray:
                    result = new SZArraySig(this.ResolveGenericArgs(typeSig.Next));
                    break;
                case ElementType.MVar:
                    result = new GenericMVar(((GenericMVar) typeSig).Number);
                    break;
                case ElementType.CModReqd:
                    result = new CModReqdSig(((ModifierSig) typeSig).Modifier, this.ResolveGenericArgs(typeSig.Next));
                    break;
                case ElementType.CModOpt:
                    result = new CModOptSig(((ModifierSig) typeSig).Modifier, this.ResolveGenericArgs(typeSig.Next));
                    break;
                case ElementType.Module:
                    result = new ModuleSig(((ModuleSig) typeSig).Index, this.ResolveGenericArgs(typeSig.Next));
                    break;
                case ElementType.Pinned:
                    result = new PinnedSig(this.ResolveGenericArgs(typeSig.Next));
                    break;
                case ElementType.FnPtr:
                    throw new NotSupportedException("FnPtr is not supported.");

                case ElementType.Array:
                    var arraySig = (ArraySig) typeSig;
                    var sizes = new List<uint>(arraySig.Sizes);
                    var lBounds = new List<int>(arraySig.LowerBounds);
                    result = new ArraySig(this.ResolveGenericArgs(typeSig.Next), arraySig.Rank, sizes, lBounds);
                    break;
                case ElementType.GenericInst:
                    var gis = (GenericInstSig) typeSig;
                    var genArgs = new List<TypeSig>(gis.GenericArguments.Count);
                    foreach (var ga in gis.GenericArguments)
                        genArgs.Add(this.ResolveGenericArgs(ga));

                    result = new GenericInstSig(this.ResolveGenericArgs(gis.GenericType) as ClassOrValueTypeSig,
                        genArgs);
                    break;

                default:
                    result = typeSig;
                    break;
            }

            this._recursionCounter.Decrement();

            return result;
        }
    }
}
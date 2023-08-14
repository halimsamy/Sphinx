using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Sphinx
{
    /// <summary>
    ///     Replaces generic type/method var with its generic argument
    /// </summary>
    internal ref struct GenericArguments
    {
        /// <summary>
        ///     Pushes generic arguments
        /// </summary>
        /// <param name="typeArgs">The generic arguments</param>
        public void PushTypeArgs(IList<TypeSig> typeArgs)
        {
            this._typeArgsStack.Push(typeArgs);
        }

        /// <summary>
        ///     Pops generic arguments
        /// </summary>
        /// <returns>The popped generic arguments</returns>
        public IList<TypeSig> PopTypeArgs()
        {
            return this._typeArgsStack.Pop();
        }

        /// <summary>
        ///     Pushes generic arguments
        /// </summary>
        /// <param name="methodArgs">The generic arguments</param>
        public void PushMethodArgs(IList<TypeSig> methodArgs)
        {
            this._methodArgsStack.Push(methodArgs);
        }

        /// <summary>
        ///     Pops generic arguments
        /// </summary>
        /// <returns>The popped generic arguments</returns>
        public IList<TypeSig> PopMethodArgs()
        {
            return this._methodArgsStack.Pop();
        }

        /// <summary>
        ///     Replaces a generic type/method var with its generic argument (if any). If
        ///     <paramref name="typeSig" /> isn't a generic type/method var or if it can't
        ///     be resolved, it itself is returned. Else the resolved type is returned.
        /// </summary>
        /// <param name="typeSig">Type signature</param>
        /// <returns>
        ///     New <see cref="TypeSig" /> which is never <c>null</c> unless
        ///     <paramref name="typeSig" /> is <c>null</c>
        /// </returns>
        public TypeSig Resolve(TypeSig typeSig)
        {
            if (typeSig == null)
                return null;

            var sig = typeSig;

            if (sig is GenericMVar genericMVar)
            {
                var newSig = this._methodArgsStack.Resolve(genericMVar.Number, false);
                if (newSig == null || newSig == sig)
                    return sig;
                return newSig;
            }

            if (sig is GenericVar genericVar)
            {
                var newSig = this._typeArgsStack.Resolve(genericVar.Number, true);
                if (newSig == null || newSig == sig)
                    return sig;
                return newSig;
            }

            return sig;
        }

        private ref struct GenericArgumentsStack
        {
            private List<IList<TypeSig>> _argsStack;

            /// <summary>
            ///     Pushes generic arguments
            /// </summary>
            /// <param name="args">The generic arguments</param>
            public void Push(IList<TypeSig> args)
            {
                (this._argsStack ??= new List<IList<TypeSig>>()).Add(args);
            }

            /// <summary>
            ///     Pops generic arguments
            /// </summary>
            /// <returns>The popped generic arguments</returns>
            public IList<TypeSig> Pop()
            {
                if (this._argsStack == null) throw new IndexOutOfRangeException();

                var index = this._argsStack.Count - 1;
                var result = this._argsStack[index];
                this._argsStack.RemoveAt(index);
                return result;
            }

            /// <summary>
            ///     Resolves a generic argument
            /// </summary>
            /// <param name="number">Generic variable number</param>
            /// <param name="isTypeVar"></param>
            /// <returns>A <see cref="TypeSig" /> or <see langword="null" /> if none was found</returns>
            public TypeSig Resolve(uint number, bool isTypeVar)
            {
                if (this._argsStack == null) return null;

                TypeSig result = null;
                for (var i = this._argsStack.Count - 1; i >= 0; i--)
                {
                    var args = this._argsStack[i];
                    if (number >= args.Count)
                        return null;
                    var typeSig = args[(int) number];
                    if (!(typeSig is GenericSig genericVar) || genericVar.IsTypeVar != isTypeVar)
                        return typeSig;
                    result = genericVar;
                    number = genericVar.Number;
                }

                return result;
            }
        }
#pragma warning disable 649 // Default value is okay.
        private GenericArgumentsStack _typeArgsStack;
        private GenericArgumentsStack _methodArgsStack;
#pragma warning restore 649
    }
}
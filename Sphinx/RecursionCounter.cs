using System;
using System.Globalization;

namespace Sphinx
{
    /// <summary>
    ///     Recursion counter
    /// </summary>
    internal ref struct RecursionCounter
    {
        /// <summary>
        ///     Max recursion count. If this is reached, we won't continue, and will use a default value.
        /// </summary>
        private const int MaxRecursionCount = 100;

        /// <summary>
        ///     Gets the recursion counter
        /// </summary>
        private int Counter { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.Counter.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     Increments <see cref="Counter" /> if it's not too high. <c>ALL</c> instance methods
        ///     that can be called recursively must call this method and <see cref="Decrement" />
        ///     (if this method returns <see langword="true" />)
        /// </summary>
        /// <returns>
        ///     <see langword="true" /> if it was incremented and caller can continue, <see langword="false" /> if
        ///     it was <c>not</c> incremented and the caller must return to its caller.
        /// </returns>
        public bool Increment()
        {
            if (this.Counter >= MaxRecursionCount)
                return false;
            this.Counter++;
            return true;
        }

        /// <summary>
        ///     Must be called before returning to caller if <see cref="Increment" />
        ///     returned <see langword="true" />.
        /// </summary>
        public void Decrement()
        {
#if DEBUG
            if (this.Counter <= 0)
                throw new InvalidOperationException("recursionCounter <= 0");
#endif
            this.Counter--;
        }
    }
}
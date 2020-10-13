namespace Sphinx
{
    /// <summary>
    ///     Various usages of Components.
    /// </summary>
    public enum ComponentUsage : byte
    {
        /// <summary>
        ///     The component is used for licensing. (should be executed first)
        /// </summary>
        Licensing = 1,

        /// <summary>
        ///     The component is used for optimizing. (should be executed second)
        /// </summary>
        Optimizing = 2,

        /// <summary>
        ///     The component is used for protecting. (should be executed third)
        /// </summary>
        Protecting = 3,


        /// <summary>
        ///     The component is used for compressing. (should be executed last)
        /// </summary>
        Compressing = 4
    }
}
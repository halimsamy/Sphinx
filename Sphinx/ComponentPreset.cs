namespace Sphinx
{
    /// <summary>
    ///     Various presets of Components.
    /// </summary>
    public enum ComponentPreset : byte
    {
        /// <summary>
        ///     The component does not belong to any preset.
        /// </summary>
        None = 0,

        /// <summary>
        ///     The component provides basic security with almost-zero performance impact.
        /// </summary>
        Minimum = 1,

        /// <summary>
        ///     The component provides normal security for public release with a minimal performance impact.
        /// </summary>
        Normal = 2,

        /// <summary>
        ///     The component provides better security with observable performance impact.
        /// </summary>
        Aggressive = 3,

        /// <summary>
        ///     The component provides strongest security with possible incompatibility and overhead performance impact.
        /// </summary>
        Maximum = 4
    }
}
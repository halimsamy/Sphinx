using System;
using System.Collections.Generic;
using System.Linq;

namespace Sphinx
{
    /// <summary>
    ///     Represent a component (a functional unit) in Sphinx.
    /// </summary>
    public abstract class Component : IEquatable<Component>, IComparable<Component>
    {
        /// <summary>
        ///     The identifier of component used by users (for the configuration file or the CLI).
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        ///     The user-friendly name of component.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        ///     The description of component.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        ///     The preset this component is in.
        /// </summary>
        public abstract ComponentPreset Preset { get; }

        /// <summary>
        ///     The usage of this component.
        /// </summary>
        public abstract ComponentUsage Usage { get; }

        /// <summary>
        ///     The priority of the component to be applied, the lowest the number is, the higher priority it has to run.
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        ///     Resolves all types that inherits <see cref="Component" />.
        /// </summary>
        /// <returns>All types of <see cref="Component" />s</returns>
        public static List<Type> Resolve()
        {
            return typeof(Program).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(Component).IsAssignableFrom(type)).ToList();
        }

        /// <summary>
        ///     Runs the component analyzing phase.
        /// </summary>
        /// <param name="ctx"></param>
        public abstract void Analyze(Context ctx);

        /// <summary>
        ///     Runs the component executing phase.
        /// </summary>
        /// <param name="ctx"></param>
        public abstract void Execute(Context ctx);

        /// <summary>
        ///     Runs the component finalizing phase.
        /// </summary>
        /// <param name="ctx"></param>
        public abstract void Finalize(Context ctx);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && this.Equals((Component) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Id, this.Name, this.Description, this.Preset, this.Usage, this.Priority);
        }


        /// <summary>
        ///     Compares the component with another, according to <see cref="Usage" /> than <see cref="Priority" />.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(Component other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var usageComparison = this.Usage.CompareTo(other.Usage);
            return usageComparison != 0 ? usageComparison : this.Priority.CompareTo(other.Priority);
        }

        public bool Equals(Component other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return this.Id == other.Id && this.Name == other.Name && this.Description == other.Description &&
                   this.Preset == other.Preset && this.Usage == other.Usage && this.Priority == other.Priority;
        }
    }
}
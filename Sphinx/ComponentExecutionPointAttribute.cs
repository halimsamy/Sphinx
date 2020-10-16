using System;

namespace Sphinx
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ComponentExecutionPointAttribute : Attribute
    {
        public readonly ExecutionPhase Phase;

        public ComponentExecutionPointAttribute(ExecutionPhase phase)
        {
            this.Phase = phase;
        }
    }
}
using System;

namespace Sphinx
{
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ComponentParamAttribute : Attribute
    {
        public readonly object DefaultValue;
        public readonly string Name;

        public ComponentParamAttribute(string name, object defaultValue)
        {
            this.Name = name;
            this.DefaultValue = defaultValue;
        }
    }
}
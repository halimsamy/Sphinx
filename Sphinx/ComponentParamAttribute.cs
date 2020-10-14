using System;

namespace Sphinx
{
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
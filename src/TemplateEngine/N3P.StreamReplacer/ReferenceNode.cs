using System.Collections.Generic;

namespace N3P.StreamReplacer
{
    public class ReferenceNode : IValueNode
    {
        private readonly string _name;
        private readonly object _defaultValue;

        public ReferenceNode(string name, object defaultValue = null)
        {
            _name = name;
            _defaultValue = defaultValue;
        }

        public object ProvideValue(IReadOnlyDictionary<string, object> args)
        {
            object value;
            if (!args.TryGetValue(_name, out value))
            {
                value = _defaultValue;
            }

            return value;
        }
    }
}
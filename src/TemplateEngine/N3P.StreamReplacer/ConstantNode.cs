using System.Collections.Generic;

namespace N3P.StreamReplacer
{
    internal class ConstantNode : IValueNode
    {
        private readonly object _value;

        public ConstantNode(object value)
        {
            _value = value;
        }

        public object ProvideValue(IReadOnlyDictionary<string, object> args)
        {
            return _value;
        }
    }
}
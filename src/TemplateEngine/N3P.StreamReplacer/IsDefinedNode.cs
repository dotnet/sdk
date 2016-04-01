using System.Collections.Generic;

namespace N3P.StreamReplacer
{
    public class IsDefinedNode : IValueNode
    {
        private readonly string _name;

        public IsDefinedNode(string name)
        {
            _name = name;
        }

        public object ProvideValue(IReadOnlyDictionary<string, object> args)
        {
            return args.ContainsKey(_name);
        }
    }
}
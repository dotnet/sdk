using System.Collections.Generic;

namespace N3P.StreamReplacer
{
    public interface IValueNode
    {
        object ProvideValue(IReadOnlyDictionary<string, object> args);
    }
}
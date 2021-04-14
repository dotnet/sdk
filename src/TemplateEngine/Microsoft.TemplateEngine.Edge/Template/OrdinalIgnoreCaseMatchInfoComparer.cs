using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Edge.Template
{
    [Obsolete("The class is deprecated")]
    public class OrdinalIgnoreCaseMatchInfoComparer : IEqualityComparer<MatchInfo>
    {
        public bool Equals(MatchInfo x, MatchInfo y)
        {
            return x.Kind == y.Kind
                && x.Location == y.Location
                && string.Equals(x.InputParameterName, y.InputParameterName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ParameterValue, y.ParameterValue, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(MatchInfo obj)
        {
            return new { a = obj.InputParameterName?.ToLowerInvariant(), b = obj.ParameterValue?.ToLowerInvariant(), obj.Kind, obj.Location }.GetHashCode();
        }
    }
}

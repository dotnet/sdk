using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public class TemplateEqualityComparer : IEqualityComparer<ITemplateInfo>
    {
        public static IEqualityComparer<ITemplateInfo> Default { get; } = new TemplateEqualityComparer();

        public bool Equals(ITemplateInfo x, ITemplateInfo y)
        {
            return ReferenceEquals(x, y) || (x != null && y != null && string.Equals(x.Identity, y.Identity, StringComparison.Ordinal));
        }

        public int GetHashCode(ITemplateInfo obj)
        {
            return obj?.Identity?.GetHashCode() ?? 0;
        }
    }
}
using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Edge.Template
{
    [Obsolete("IFilteredTemplateInfo is obsolete")]
    public class FilteredTemplateEqualityComparer : IEqualityComparer<IFilteredTemplateInfo>
    {
        public static IEqualityComparer<IFilteredTemplateInfo> Default { get; } = new FilteredTemplateEqualityComparer();

        public bool Equals(IFilteredTemplateInfo x, IFilteredTemplateInfo y)
        {
            return ReferenceEquals(x.Info, y.Info) || (x != null && y != null && x.Info != null && y.Info != null && string.Equals(x.Info.Identity, y.Info.Identity, StringComparison.Ordinal));
        }

        public int GetHashCode(IFilteredTemplateInfo obj)
        {
            return obj?.Info?.Identity?.GetHashCode() ?? 0;
        }
    }
}
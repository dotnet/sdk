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

    // Compares the templates, irrespective of the match result
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
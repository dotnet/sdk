// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Edge.Template
{
    [Obsolete("This class is deprecated.")]
    public class TemplateMatchInfoEqualityComparer : IEqualityComparer<ITemplateMatchInfo>
    {
        public static IEqualityComparer<ITemplateMatchInfo> Default { get; } = new TemplateMatchInfoEqualityComparer();

        public bool Equals(ITemplateMatchInfo x, ITemplateMatchInfo y)
        {
            return ReferenceEquals(x?.Info, y?.Info) || (x != null && y != null && x?.Info != null && y?.Info != null && string.Equals(x?.Info?.Identity, y?.Info?.Identity, StringComparison.Ordinal));
        }

        public int GetHashCode(ITemplateMatchInfo obj)
        {
            return obj?.Info?.Identity?.GetHashCode() ?? 0;
        }
    }
}

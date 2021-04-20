// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Template
{
    [Obsolete("This class is deprecated.")]
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
}


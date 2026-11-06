// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Results
{
    internal class TemplateIdentityEqualityComparer : IEqualityComparer<ITemplateInfo>
    {
        public bool Equals(ITemplateInfo? x, ITemplateInfo? y)
        {
            if (x == null && y == null)
            {
                return true;
            }
            if (x == null || y == null)
            {
                return false;
            }
            return string.Equals(x.Identity, y.Identity, StringComparison.Ordinal);
        }

        public int GetHashCode(ITemplateInfo obj)
        {
            return obj.Identity.GetHashCode();
        }
    }
}

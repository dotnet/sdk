// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.Common
{
    public class TemplateIdentificationEntry
    {
        public TemplateIdentificationEntry(string identity, string groupIdentity)
        {
            Identity = identity;
            GroupIdentity = groupIdentity;
        }

        public string Identity { get; }
        public string GroupIdentity { get; }
    }
}

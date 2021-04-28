// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    public interface IPackInfo
    {
        string Id { get; }

        string Version { get; }

        long TotalDownloads { get; }
    }
}

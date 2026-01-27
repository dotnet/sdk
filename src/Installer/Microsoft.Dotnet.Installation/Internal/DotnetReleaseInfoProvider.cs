// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

internal class DotnetReleaseInfoProvider : IDotnetReleaseInfoProvider
{
    public IEnumerable<string> GetSupportedChannels(bool includeFeatureBands = true)
    {
        var releaseManifest = new ChannelVersionResolver();
        return releaseManifest.GetSupportedChannels(includeFeatureBands);
    }
    public ReleaseVersion? GetLatestVersion(InstallComponent component, string channel)
    {
        var releaseManifest = new ChannelVersionResolver();
        var release = releaseManifest.GetLatestVersionForChannel(new UpdateChannel(channel), component);

        return release;
    }
    public SupportType GetSupportType(InstallComponent component, ReleaseVersion version) => throw new NotImplementedException();
}

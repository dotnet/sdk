// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

internal class DotnetReleaseInfoProvider : IDotnetReleaseInfoProvider
{
    private readonly ChannelVersionResolver _resolver = new();

    public IEnumerable<string> GetSupportedChannels(bool includeFeatureBands = true)
    {
        return _resolver.GetSupportedChannels(includeFeatureBands);
    }
    public ReleaseVersion? GetLatestVersion(InstallComponent component, string channel)
    {
        return _resolver.GetLatestVersionForChannel(new UpdateChannel(channel), component);
    }
    public SupportType GetSupportType(InstallComponent component, ReleaseVersion version) => throw new NotImplementedException();
}

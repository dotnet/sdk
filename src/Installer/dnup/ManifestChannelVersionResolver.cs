// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ManifestChannelVersionResolver
{
    public DotnetInstall Resolve(DotnetInstallRequest dotnetChannelVersion)
    {
        string channel = dotnetChannelVersion.ChannelVersion;
        DotnetVersion dotnetVersion = new DotnetVersion(channel);

        // If not fully specified, resolve to latest using ReleaseManifest
        if (!dotnetVersion.IsFullySpecified)
        {
            var manifest = new ReleaseManifest();
            var latestVersion = manifest.GetLatestVersionForChannel(channel, dotnetChannelVersion.Mode);
            if (latestVersion != null)
            {
                dotnetVersion = new DotnetVersion(latestVersion);
            }
        }

        return new DotnetInstall(
            dotnetVersion,
            dotnetChannelVersion.ResolvedDirectory,
            dotnetChannelVersion.Type,
            dotnetChannelVersion.Mode,
            dotnetChannelVersion.Architecture,
            dotnetChannelVersion.Cadence);
    }
}

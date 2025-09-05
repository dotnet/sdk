// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ManifestChannelVersionResolver
{
    public DotnetInstall Resolve(DotnetInstallRequest dotnetChannelVersion)
    {
        string fullySpecifiedVersion = dotnetChannelVersion.ChannelVersion;

        DotnetVersion dotnetVersion = new DotnetVersion(fullySpecifiedVersion);

        // Resolve strings or other options
        if (!dotnetVersion.IsValidMajorVersion())
        {
        // TODO ping the r-manifest to handle 'lts' 'latest' etc
        // Do this in a separate class and use dotnet release library to do so
        // https://github.com/dotnet/deployment-tools/tree/main/src/Microsoft.Deployment.DotNet.Releases
        }

        // Make sure the version is fully specified
        if (!dotnetVersion.IsFullySpecified)
        {
            // TODO ping the r-manifest to resolve latest within the specified qualities
        }

        return new DotnetInstall(
            fullySpecifiedVersion,
            dotnetChannelVersion.ResolvedDirectory,
            dotnetChannelVersion.Type,
            dotnetChannelVersion.Mode,
            dotnetChannelVersion.Architecture,
            dotnetChannelVersion.Cadence);
    }
}

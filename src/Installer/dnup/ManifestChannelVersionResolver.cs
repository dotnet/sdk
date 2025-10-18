// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ManifestChannelVersionResolver
{
    public ReleaseVersion? Resolve(DotnetInstallRequest installRequest)
    {
        // If not fully specified, resolve to latest using ReleaseManifest
        if (!installRequest.Channel.IsFullySpecifiedVersion())
        {
            var manifest = new ReleaseManifest();
            return manifest.GetLatestVersionForChannel(installRequest.Channel, installRequest.Component);
        }

        return new ReleaseVersion(installRequest.Channel.Name);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ManifestChannelVersionResolver
{
    public DotnetInstall Resolve(DotnetInstallRequest dotnetChannelVersion)
    {
        // TODO: Implement logic to resolve the channel version from the manifest
        // For now, return a placeholder
        return new DotnetInstall(
            "TODO_RESOLVED_VERSION",
            dotnetChannelVersion.ResolvedDirectory,
            dotnetChannelVersion.Type,
            dotnetChannelVersion.Mode,
            dotnetChannelVersion.Architecture);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class DnupSharedManifest : IDnupManifest
{
    public IEnumerable<DotnetInstall> GetInstalledVersions()
    {
        return [];
    }

    public void AddInstalledVersion(DotnetInstall version)
    {
    }

    public void RemoveInstalledVersion(DotnetInstall version)
    {
    }
}

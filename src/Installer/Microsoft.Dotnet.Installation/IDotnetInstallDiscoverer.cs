// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation;

public interface IDotnetInstallDiscoverer
{
    DotnetInstallRoot GetDotnetInstallRootFromPath();

    IEnumerable<ReleaseVersion> GetInstalledVersions(DotnetInstallRoot dotnetRoot, InstallComponent component);
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    internal interface IDnupManifest
    {
        IEnumerable<DotnetInstall> GetInstalledVersions(IInstallationValidator? validator = null);
        IEnumerable<DotnetInstall> GetInstalledVersions(DotnetInstallRoot installRoot, IInstallationValidator? validator = null);
        void AddInstalledVersion(DotnetInstall version);
        void RemoveInstalledVersion(DotnetInstall version);
    }
}

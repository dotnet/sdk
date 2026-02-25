// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal interface IDotnetupManifest
{
    IEnumerable<DotnetInstall> GetInstalledVersions(IInstallationValidator? validator = null);
    IEnumerable<DotnetInstall> GetInstalledVersions(DotnetInstallRoot installRoot, IInstallationValidator? validator = null);
    void AddInstalledVersion(DotnetInstall version);
    void RemoveInstalledVersion(DotnetInstall version);

    IEnumerable<InstallSpec> GetInstallSpecs(DotnetInstallRoot installRoot);
    void AddInstallSpec(DotnetInstallRoot installRoot, InstallSpec spec);
    void RemoveInstallSpec(DotnetInstallRoot installRoot, InstallSpec spec);

    IEnumerable<Installation> GetInstallations(DotnetInstallRoot installRoot);
    void AddInstallation(DotnetInstallRoot installRoot, Installation installation);
    void RemoveInstallation(DotnetInstallRoot installRoot, Installation installation);
}

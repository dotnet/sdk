// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation;

public interface IDotnetInstaller
{
    void Install(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version);
    void Uninstall(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version);
}

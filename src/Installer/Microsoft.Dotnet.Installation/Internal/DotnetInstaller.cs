// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

internal class DotnetInstaller : IDotnetInstaller
{
    private readonly IProgressTarget _progressTarget;

    public DotnetInstaller(IProgressTarget progressTarget)
    {
        _progressTarget = progressTarget;
    }

    public void Install(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version)
    {
        using var op = InstallationActivitySource.StartTracked("DotnetInstaller.Install", "install/complete");
        op.SetTag("install.root", dotnetRoot.Path);
        op.SetTag("install.arch", dotnetRoot.Architecture.ToString());
        op.SetTag("install.component", component.ToString());
        op.SetTag("install.version", version.ToString());

        var installRequest = new DotnetInstallRequest(dotnetRoot, new UpdateChannel(version.ToString()), component, new InstallRequestOptions());

        using DotnetArchiveExtractor installer = new(installRequest, version, new ReleaseManifest(), _progressTarget);
        installer.Prepare();
        installer.Commit();
    }
    public void Uninstall(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version) => throw new NotImplementedException();
}

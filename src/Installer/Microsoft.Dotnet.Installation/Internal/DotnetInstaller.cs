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
        using var op = Metrics.Track("DotnetInstaller.Install", "install/complete");
        op.Tag("install.root", dotnetRoot.Path);
        op.Tag("install.arch", dotnetRoot.Architecture.ToString());
        op.Tag("install.component", component.ToString());
        op.Tag("install.version", version.ToString());

        var installRequest = new DotnetInstallRequest(dotnetRoot, new UpdateChannel(version.ToString()), component, new InstallRequestOptions());

        using DotnetArchiveExtractor installer = new(installRequest, version, new ReleaseManifest(), _progressTarget);
        installer.Prepare();
        installer.Commit();
    }
    public void Uninstall(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version) => throw new NotImplementedException();
}

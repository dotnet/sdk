using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation;

public interface IDotnetInstaller
{
    void Install(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version);
    void Uninstall(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version);
}

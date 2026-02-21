using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation;

public interface IDotnetInstaller
{
    Task InstallAsync(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version);
    void Uninstall(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version);
}

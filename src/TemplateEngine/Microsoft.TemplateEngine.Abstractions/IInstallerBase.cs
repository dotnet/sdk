using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IInstallerBase
    {
        void InstallPackages(IEnumerable<string> installationRequests);

        IEnumerable<string> Uninstall(IEnumerable<string> uninstallRequests);
    }
}

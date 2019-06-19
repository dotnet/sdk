using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IInstallerBase
    {
        void InstallPackages(IEnumerable<string> installationRequests);

        IEnumerable<string> Uninstall(IEnumerable<string> uninstallRequests);
    }
}

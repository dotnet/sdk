using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.IDE
{
    public interface IInstaller : IInstallerBase
    {
        // These interface methods from IInstallerBase are being explictly overridden here so as not to introduce
        // an avoidable breaking change to IDE hosts. These were originally on this interface, but then moved
        // to the base interface.
        new void InstallPackages(IEnumerable<string> installationRequests);

        new IEnumerable<string> Uninstall(IEnumerable<string> uninstallRequests);
    }
}

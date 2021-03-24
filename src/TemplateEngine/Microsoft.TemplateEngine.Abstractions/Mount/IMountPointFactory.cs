using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IMountPointFactory : IIdentifiedComponent
    {
        bool TryMount(IEngineEnvironmentSettings environmentSettings, IMountPoint parent, string mountPointUri, out IMountPoint mountPoint);
    }
}

using System;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IMountPointManager
    {
        IEngineEnvironmentSettings EnvironmentSettings { get; }

        bool TryDemandMountPoint(string mountPointUri, out IMountPoint mountPoint);
    }
}

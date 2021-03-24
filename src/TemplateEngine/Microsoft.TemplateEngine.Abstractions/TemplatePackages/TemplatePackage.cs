using System;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackages
{
    /// <summary>
    /// This is basic <see cref="ITemplatePackage"/> implementation so each
    /// <see cref="ITemplatePackagesProvider"/> doesn't need to re-implement.
    /// </summary>
    public class TemplatePackage : ITemplatePackage
    {
        public TemplatePackage(ITemplatePackagesProvider provider, string mountPointUri, DateTime lastChangeTime)
        {
            Provider = provider;
            MountPointUri = mountPointUri;
            LastChangeTime = lastChangeTime;
        }

        public ITemplatePackagesProvider Provider { get; }

        public string MountPointUri { get; }

        public DateTime LastChangeTime { get; }
    }
}

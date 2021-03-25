using System;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// This is basic <see cref="ITemplatePackage"/> implementation so each
    /// <see cref="ITemplatePackageProvider"/> doesn't need to re-implement.
    /// </summary>
    public class TemplatePackage : ITemplatePackage
    {
        public TemplatePackage(ITemplatePackageProvider provider, string mountPointUri, DateTime lastChangeTime)
        {
            Provider = provider;
            MountPointUri = mountPointUri;
            LastChangeTime = lastChangeTime;
        }

        public ITemplatePackageProvider Provider { get; }

        public string MountPointUri { get; }

        public DateTime LastChangeTime { get; }
    }
}

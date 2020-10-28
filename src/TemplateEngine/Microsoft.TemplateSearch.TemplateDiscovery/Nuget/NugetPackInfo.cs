using Microsoft.TemplateSearch.TemplateDiscovery.PackProviders;

namespace Microsoft.TemplateSearch.TemplateDiscovery.Nuget
{
    public class NugetPackInfo : IInstalledPackInfo
    {
        public string VersionedPackageIdentity { get; set; }

        public string Id { get; set; }

        public string Version { get; set; }

        public string Path { get; set; }

        public long TotalDownloads { get; set; }
    }
}

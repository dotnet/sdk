namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    public interface IInstalledPackInfo
    {
        /// <summary>
        /// The fully qualified Id. Style may vary from source to source.
        /// </summary>
        string VersionedPackageIdentity { get; }

        string Id { get; }

        string Version { get; }

        /// <summary>
        /// The path on disk for the pack.
        /// </summary>
        string Path { get; }
    }
}

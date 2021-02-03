namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    public interface IDownloadedPackInfo : IPackInfo
    {
        /// <summary>
        /// The fully qualified Id. Style may vary from source to source.
        /// </summary>
        string VersionedPackageIdentity { get; }

        /// <summary>
        /// The path on disk for the pack.
        /// </summary>
        string Path { get; }
    }
}

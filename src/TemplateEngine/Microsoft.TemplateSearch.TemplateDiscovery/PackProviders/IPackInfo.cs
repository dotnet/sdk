
namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    public interface IPackInfo
    {
        string Id { get; }
        string Version { get; }
        long TotalDownloads { get; }
    }
}

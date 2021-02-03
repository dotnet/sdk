using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    public interface IPackProvider
    {
        string Name { get; }

        IAsyncEnumerable<IPackInfo> GetCandidatePacksAsync();

        Task<IDownloadedPackInfo> DownloadPackageAsync(IPackInfo packinfo);

        Task<int> GetPackageCountAsync();

        void DeleteDownloadedPacks();
    }
}

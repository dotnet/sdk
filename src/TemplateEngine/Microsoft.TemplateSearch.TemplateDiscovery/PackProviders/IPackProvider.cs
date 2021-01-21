using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.TemplateSearch.TemplateDiscovery.PackProviders
{
    public interface IPackProvider
    {
        IAsyncEnumerable<IInstalledPackInfo> GetCandidatePacksAsync();

        Task<int> GetPackageCountAsync();

        void DeleteDownloadedPacks();
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackages
{
    public interface ITemplatePackagesProvider
    {

        Task<IReadOnlyList<ITemplatePackage>> GetAllSourcesAsync(CancellationToken cancellationToken);


        event Action SourcesChanged;


        ITemplatePackagesProviderFactory Factory { get; }
    }
}

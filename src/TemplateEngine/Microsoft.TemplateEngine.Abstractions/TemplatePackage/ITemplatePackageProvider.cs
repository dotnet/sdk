using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    public interface ITemplatePackageProvider
    {

        Task<IReadOnlyList<ITemplatePackage>> GetAllSourcesAsync(CancellationToken cancellationToken);


        event Action SourcesChanged;


        ITemplatePackageProviderFactory Factory { get; }
    }
}

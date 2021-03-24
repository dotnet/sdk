using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackages
{
    public interface ITemplatePackagesManager
    {
        /// <summary>
        /// Triggered every time when list of <see cref="ITemplatePackage"/> changes, this is triggered by <see cref="ITemplatePackagesProvider.SourcesChanged"/>.
        /// </summary>
        event Action SourcesChanged;

        /// <summary>
        /// Returns combined list of <see cref="ITemplatePackage"/> that all <see cref="ITemplatePackagesProvider"/>s and <see cref="IManagedTemplatePackagesProvider"/>s return.
        /// </summary>
        /// <param name="force">Invalidates cache and queries all providers.</param>
        /// <returns></returns>
        Task<IReadOnlyList<ITemplatePackage>> GetTemplatePackages(bool force = false);

        /// <summary>
        /// This is same as <see cref="GetTemplatePackages"/> but filters only <see cref="IManagedTemplatePackage"/> types of sources.
        /// </summary>
        /// <param name="force">Invalidates cache and queries all providers.</param>
        /// <returns></returns>
        Task<IReadOnlyList<IManagedTemplatePackage>> GetManagedTemplatePackages(bool force = false);

        /// <summary>
        /// This is helper method for <see cref="GetManagedTemplatePackages"/> with <see cref="System.Linq.Enumerable.GroupBy"/>
        /// </summary>
        /// <param name="force">If true, invalidates cache</param>
        /// <returns></returns>
        Task<IReadOnlyList<(IManagedTemplatePackagesProvider Provider, IReadOnlyList<IManagedTemplatePackage> ManagedSources)>> GetManagedSourcesGroupedByProvider(bool force = false);

        /// <summary>
        /// Returns <see cref="IManagedTemplatePackagesProvider"/> with specified name
        /// </summary>
        /// <param name="name">Name from <see cref="ITemplatePackagesProviderFactory.Name"/>.</param>
        /// <returns></returns>
        IManagedTemplatePackagesProvider GetManagedProvider(string name);

        /// <summary>
        /// Returns <see cref="IManagedTemplatePackagesProvider"/> with specified Guid
        /// </summary>
        /// <param name="id">Guid from <see cref="IIdentifiedComponent.Id"/> of <see cref="ITemplatePackagesProviderFactory"/>.</param>
        /// <returns></returns>
        IManagedTemplatePackagesProvider GetManagedProvider(Guid id);
    }
}

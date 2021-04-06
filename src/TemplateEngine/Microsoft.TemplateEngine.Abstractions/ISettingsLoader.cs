using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// Responsible for loading/storing settings, handling of template cache, mount points.
    /// </summary>
    public interface ISettingsLoader
    {
        /// <summary>
        /// Component manager for this instance of settings.
        /// </summary>
        IComponentManager Components { get; }

        /// <summary>
        /// Parent engine environment settings, used to create this settings loader.
        /// </summary>
        IEngineEnvironmentSettings EnvironmentSettings { get; }

        /// <summary>
        /// Manages all <see cref="ITemplatePackageProvider"/> available to the host.
        /// </summary>
        ITemplatePackageManager TemplatePackagesManager { get; }

        /// <summary>
        /// Adds path to be scanned by <see cref="IComponentManager"/> when looking up assemblies for components.
        /// </summary>
        /// <param name="probeIn">Absolute path to be probed.</param>
        void AddProbingPath(string probeIn);

        /// <summary>
        /// Gets all templates based on current settings.
        /// </summary>
        /// <remarks>
        /// This call is cached. And can be invalidated by <see cref="RebuildCacheAsync"/>.
        /// </remarks>
        Task<IReadOnlyList<ITemplateInfo>> GetTemplatesAsync(CancellationToken token);

        /// <summary>
        /// Fully load template from <see cref="ITemplateInfo"/>.
        /// <see cref="ITemplateInfo"/> usually comes from cache and is missing some information.
        /// Calling this methods returns full information about template needed to instantiate template.
        /// </summary>
        /// <param name="info">Information about template.</param>
        /// <param name="baselineName">Defines which baseline of template to load.</param>
        /// <returns>Fully loaded template.</returns>
        ITemplate LoadTemplate(ITemplateInfo info, string baselineName);

        /// <summary>
        /// Saves settings to file.
        /// </summary>
        void Save();

        /// <summary>
        /// Loads <see cref="IMountPoint"/> via <see cref="IMountPointFactory"/> that are
        /// loaded in <see cref="IComponentManager"/>.
        /// </summary>
        /// <param name="mountPointUri">Uri to load.</param>
        /// <param name="mountPoint">Mountpoint to be returned.</param>
        /// <returns><c>true</c> if mountpoint was loaded.</returns>
        bool TryGetMountPoint(string mountPointUri, out IMountPoint mountPoint);

        /// <summary>
        /// Finds best host file, based on <see cref="ITemplateEngineHost.FallbackHostTemplateConfigNames"/>.
        /// </summary>
        /// <param name="config">File that represents original template file.</param>
        /// <returns>Host file if exists; otherwise <c>null</c>.</returns>
        IFile FindBestHostTemplateConfigFile(IFileSystemInfo config);

        /// <summary>
        /// Deletes templates cache and rebuilds it.
        /// Useful if user suspects cache is corrupted and wants to rebuild it.
        /// </summary>
        Task RebuildCacheAsync(CancellationToken token);
    }
}

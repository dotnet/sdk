// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
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
        /// Gets the templates filtered using <paramref name="filters"/> and <paramref name="matchCriteria"/>.
        /// </summary>
        /// <param name="matchCriteria">The criteria for <see cref="ITemplateMatchInfo"/> to be included to result collection.</param>
        /// <param name="filters">The list of filters to be applied to templates.</param>
        /// <returns>The filtered list of templates with match information.</returns>
        /// <example>
        /// <c>GetTemplatesAsync(WellKnownSearchFilters.MatchesAllCriteria, new [] { WellKnownSearchFilters.NameFilter("myname") }</c> - returns the templates which name or short name contains "myname". <br/>
        /// <c>GetTemplatesAsync(TemplateListFilter.MatchesAtLeastOneCriteria, new [] { WellKnownSearchFilters.NameFilter("myname"), WellKnownSearchFilters.NameFilter("othername") })</c> - returns the templates which name or short name contains "myname" or "othername".<br/>
        /// </example>
        Task<IReadOnlyList<ITemplateMatchInfo>> GetTemplatesAsync(Func<ITemplateMatchInfo, bool> matchCriteria, IEnumerable<Func<ITemplateInfo, MatchInfo?>> filters, CancellationToken token = default);

        /// <summary>
        /// Fully load template from <see cref="ITemplateInfo"/>.
        /// <see cref="ITemplateInfo"/> usually comes from cache and is missing some information.
        /// Calling this methods returns full information about template needed to instantiate template.
        /// </summary>
        /// <param name="info">Information about template.</param>
        /// <param name="baselineName">Defines which baseline of template to load.</param>
        /// <returns>Fully loaded template or <c>null</c> if it fails to load template.</returns>
        ITemplate? LoadTemplate(ITemplateInfo info, string baselineName);

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
        IFile? FindBestHostTemplateConfigFile(IFileSystemInfo config);

        /// <summary>
        /// Deletes templates cache and rebuilds it.
        /// Useful if user suspects cache is corrupted and wants to rebuild it.
        /// </summary>
        Task RebuildCacheAsync(CancellationToken token);

        /// <summary>
        /// Resets settings of host version.
        /// Useful when the settings need to be reset to default and all caches to be reinitialized.
        /// </summary>
        void ResetHostSettings();
    }
}

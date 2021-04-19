// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using TemplateCreationResult = Microsoft.TemplateEngine.Edge.Template.TemplateCreationResult;
using TemplateCreator = Microsoft.TemplateEngine.Edge.Template.TemplateCreator;

namespace Microsoft.TemplateEngine.IDE
{
    public class Bootstrapper
    {
        private readonly ITemplateEngineHost _host;
        private readonly Action<IEngineEnvironmentSettings> _onFirstRun;
        private readonly Paths _paths;
        private readonly TemplateCreator _templateCreator;

        private EngineEnvironmentSettings EnvironmentSettings { get; }

        public Bootstrapper(ITemplateEngineHost host, Action<IEngineEnvironmentSettings> onFirstRun, bool virtualizeConfiguration)
        {
            _host = host;
            EnvironmentSettings = new EngineEnvironmentSettings(host, x => new SettingsLoader(x));
            _onFirstRun = onFirstRun;
            _paths = new Paths(EnvironmentSettings);
            _templateCreator = new TemplateCreator(EnvironmentSettings);

            if (virtualizeConfiguration)
            {
                EnvironmentSettings.Host.VirtualizeDirectory(EnvironmentSettings.Paths.TemplateEngineRootDir);
            }
        }

        private void EnsureInitialized()
        {
            if (!_paths.Exists(_paths.User.BaseDir) || !_paths.Exists(_paths.User.FirstRunCookie))
            {
                _onFirstRun?.Invoke(EnvironmentSettings);
                _paths.WriteAllText(_paths.User.FirstRunCookie, "");
            }
        }

        public void Register(Type type)
        {
            EnvironmentSettings.SettingsLoader.Components.Register(type);
        }

        public void Register(Assembly assembly)
        {
            EnvironmentSettings.SettingsLoader.Components.RegisterMany(assembly.GetTypes());
        }

        [Obsolete("Use " + nameof(GetTemplatesAsync) + "instead")]
        public async Task<IReadOnlyCollection<Edge.Template.IFilteredTemplateInfo>> ListTemplates(bool exactMatchesOnly, params Func<ITemplateInfo, Edge.Template.MatchInfo?>[] filters)
        {
            EnsureInitialized();
            return TemplateListFilter.FilterTemplates(await EnvironmentSettings.SettingsLoader.GetTemplatesAsync(default).ConfigureAwait(false), exactMatchesOnly, filters);
        }

        /// <summary>
        /// Gets list of available templates, if <paramref name="filters"/> are provided returns only matching templates.
        /// </summary>
        /// <param name="filters">List of filters to apply. See <see cref="WellKnownSearchFilters"/> for predefined filters.</param>
        /// <param name="exactMatchesOnly">
        /// true: templates should match all filters; false: templates should match any filter.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns>Filtered list of available templates with details on the applied filters matches.</returns>
        public Task<IReadOnlyList<ITemplateMatchInfo>> GetTemplatesAsync(IEnumerable<Func<ITemplateInfo, MatchInfo?>>? filters = null, bool exactMatchesOnly = true, CancellationToken cancellationToken = default)
        {
            Func<ITemplateMatchInfo, bool> criteria = exactMatchesOnly ? WellKnownSearchFilters.MatchesAllCriteria : WellKnownSearchFilters.MatchesAtLeastOneCriteria;
            EnsureInitialized();
            return EnvironmentSettings.SettingsLoader.GetTemplatesAsync(criteria, filters ?? Array.Empty<Func<ITemplateInfo, MatchInfo?>>(), cancellationToken);
        }

        public async Task<ICreationResult> CreateAsync(ITemplateInfo info, string name, string outputPath, IReadOnlyDictionary<string, string> parameters, bool skipUpdateCheck, string baselineName)
        {
            TemplateCreationResult instantiateResult = await _templateCreator.InstantiateAsync(info, name, name, outputPath, parameters, skipUpdateCheck, false, baselineName).ConfigureAwait(false);
            return instantiateResult.ResultInfo;
        }

        public async Task<ICreationEffects> GetCreationEffectsAsync(ITemplateInfo info, string name, string outputPath, IReadOnlyDictionary<string, string> parameters, string baselineName)
        {
            TemplateCreationResult instantiateResult = await _templateCreator.InstantiateAsync(info, name, name, outputPath, parameters, true, false, baselineName, true).ConfigureAwait(false);
            return instantiateResult.CreationEffects;
        }

        #region Template Package Management

        /// <summary>
        /// Gets the list of available template packages.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>the list of the template packages.</returns>
        public Task<IReadOnlyList<ITemplatePackage>> GetTemplatePackages(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();
            return EnvironmentSettings.SettingsLoader.TemplatePackagesManager.GetTemplatePackagesAsync();
        }

        /// <summary>
        /// Gets the list of available managed template packages.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>the list of managed template packages.</returns>
        public Task<IReadOnlyList<IManagedTemplatePackage>> GetManagedTemplatePackages(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();
            return EnvironmentSettings.SettingsLoader.TemplatePackagesManager.GetManagedTemplatePackagesAsync();
        }

        /// <summary>
        /// Installs the template packages
        /// The following template packages are supported by default:
        /// - the NuGet package from NuGet feed
        /// - the NuGet package available at the path
        /// - the folder containing the template.
        /// </summary>
        /// <param name="installRequests">the list of <see cref="InstallRequest"/> to install.</param>
        /// <param name="scope"><see cref="InstallationScope"/> to use.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>the list of <see cref="InstallResult"/> containing installation result for each <see cref="InstallRequest"/>.</returns>
        public Task<IReadOnlyList<InstallResult>> InstallTemplatePackagesAsync(IEnumerable<InstallRequest> installRequests, InstallationScope scope = InstallationScope.Global, CancellationToken cancellationToken = default)
        {
            _ = installRequests ?? throw new ArgumentNullException(nameof(installRequests));
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();

            if (!installRequests.Any())
            {
                return Task.FromResult((IReadOnlyList<InstallResult>)new List<InstallResult>());
            }

            IManagedTemplatePackageProvider managedPackageProvider;
            switch (scope)
            {
                case InstallationScope.Global:
                default:
                    {
                        managedPackageProvider = EnvironmentSettings.SettingsLoader.TemplatePackagesManager.GetBuiltInManagedProvider(InstallationScope.Global);
                        break;
                    }
            };

            return managedPackageProvider.InstallAsync(installRequests, cancellationToken);
        }

        /// <summary>
        /// Gets the latest template package version for <paramref name="managedPackages"/>.
        /// </summary>
        /// <param name="managedPackages">the template packages to check the version for.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>the list of <see cref="CheckUpdateResult"/> containing the result for each <see cref="IManagedTemplatePackage"/>.</returns>
        public async Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionsAsync(IEnumerable<IManagedTemplatePackage> managedPackages, CancellationToken cancellationToken = default)
        {
            _ = managedPackages ?? throw new ArgumentNullException(nameof(managedPackages));
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();

            if (!managedPackages.Any())
            {
                return new List<CheckUpdateResult>();
            }

            IEnumerable<IGrouping<IManagedTemplatePackageProvider, IManagedTemplatePackage>> requestsGroupedByProvider = managedPackages.GroupBy(package => package.ManagedProvider, package => package);
            IReadOnlyList<CheckUpdateResult>[] results = await Task.WhenAll(requestsGroupedByProvider.Select(packages => packages.Key.GetLatestVersionsAsync(packages, cancellationToken))).ConfigureAwait(false);

            return results.SelectMany(result => result).ToList();
        }

        /// <summary>
        /// Updates the template packages to version specified in <see cref="UpdateRequest"/>.
        /// </summary>
        /// <param name="updateRequests">the list of <see cref="UpdateRequest"/> to perform.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>the list of <see cref="UpdateResult"/> containing the result for each <see cref="UpdateRequest"/>.</returns>
        public async Task<IReadOnlyList<UpdateResult>> UpdateTemplatePackagesAsync(IEnumerable<UpdateRequest> updateRequests, CancellationToken cancellationToken = default)
        {
            _ = updateRequests ?? throw new ArgumentNullException(nameof(updateRequests));
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();

            if (!updateRequests.Any())
            {
                return new List<UpdateResult>();
            }

            IEnumerable<IGrouping<IManagedTemplatePackageProvider, UpdateRequest>> requestsGroupedByProvider = updateRequests.GroupBy(request => request.TemplatePackage.ManagedProvider, request => request);
            IReadOnlyList<UpdateResult>[] updateResults = await Task.WhenAll(requestsGroupedByProvider.Select(requests => requests.Key.UpdateAsync(requests, cancellationToken))).ConfigureAwait(false);

            return updateResults.SelectMany(result => result).ToList();
        }

        /// <summary>
        /// Uninstalls the template packages.
        /// </summary>
        /// <param name="managedPackages">the list of <see cref="IManagedTemplatePackage"/> to uninstall.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>the list of <see cref="UninstallResult"/> containing the result for each <see cref="IManagedTemplatePackage"/>.</returns>
        public async Task<IReadOnlyList<UninstallResult>> UninstallTemplatePackagesAsync(IEnumerable<IManagedTemplatePackage> managedPackages, CancellationToken cancellationToken = default)
        {
            _ = managedPackages ?? throw new ArgumentNullException(nameof(managedPackages));
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();

            if (!managedPackages.Any())
            {
                return new List<UninstallResult>();
            }

            IEnumerable<IGrouping<IManagedTemplatePackageProvider, IManagedTemplatePackage>> requestsGroupedByProvider = managedPackages.GroupBy(package => package.ManagedProvider, package => package);
            IReadOnlyList<UninstallResult>[] uninstallResults = await Task.WhenAll(requestsGroupedByProvider.Select(packages => packages.Key.UninstallAsync(packages, cancellationToken))).ConfigureAwait(false);

            return uninstallResults.SelectMany(result => result).ToList();
        }

        #endregion

        #region Obsolete
        [Obsolete("use Task<IReadOnlyList<InstallResult>> InstallTemplatePackagesAsync(IEnumerable<InstallRequest> installRequests, InstallationScope scope = InstallationScope.Global, CancellationToken cancellationToken = default) instead")]
        public void Install(string path)
        {
            Install(new[] { path });
        }

        [Obsolete("use Task<IReadOnlyList<InstallResult>> InstallTemplatePackagesAsync(IEnumerable<InstallRequest> installRequests, InstallationScope scope = InstallationScope.Global, CancellationToken cancellationToken = default) instead")]
        public void Install(params string[] paths)
        {
            Install((IEnumerable<string>)paths);
        }

        [Obsolete("use Task<IReadOnlyList<InstallResult>> InstallTemplatePackagesAsync(IEnumerable<InstallRequest> installRequests, InstallationScope scope = InstallationScope.Global, CancellationToken cancellationToken = default) instead")]
        public void Install(IEnumerable<string> paths)
        {
            _ = paths ?? throw new ArgumentNullException(nameof(paths));
            EnsureInitialized();

            if (!paths.Any())
            {
                return;
            }

            var installRequests = paths.Select(path => new InstallRequest(path)).ToList();
            Task<IReadOnlyList<InstallResult>> t = InstallTemplatePackagesAsync(installRequests);
            t.Wait();
        }

        [Obsolete("use Task<IReadOnlyList<UninstallResult>> UninstallTemplatePackagesAsync(IEnumerable<IManagedTemplatePackage> managedPackages, CancellationToken cancellationToken = default) instead")]
        public IEnumerable<string> Uninstall(string path)
        {
            return Uninstall(new[] { path });
        }

        [Obsolete("use Task<IReadOnlyList<UninstallResult>> UninstallTemplatePackagesAsync(IEnumerable<IManagedTemplatePackage> managedPackages, CancellationToken cancellationToken = default) instead")]
        public IEnumerable<string> Uninstall(params string[] paths)
        {
            return Uninstall((IEnumerable<string>)paths);
        }

        [Obsolete("use Task<IReadOnlyList<UninstallResult>> UninstallTemplatePackagesAsync(IEnumerable<IManagedTemplatePackage> managedPackages, CancellationToken cancellationToken = default) instead")]
        public IEnumerable<string> Uninstall(IEnumerable<string> paths)
        {
            _ = paths ?? throw new ArgumentNullException(nameof(paths));
            EnsureInitialized();

            if (!paths.Any())
            {
                return Array.Empty<string>();
            }

            var task = GetManagedTemplatePackages();
            task.Wait();
            var templatePackages = task.Result;

            var packagesToUninstall = new List<IManagedTemplatePackage>();
            foreach (string path in paths)
            {
                packagesToUninstall.AddRange(templatePackages.Where(package => package.Identifier.Equals(path, StringComparison.OrdinalIgnoreCase)));
            }

            Task<IReadOnlyList<UninstallResult>> uninstallTask = UninstallTemplatePackagesAsync(packagesToUninstall);
            uninstallTask.Wait();
            return uninstallTask.Result.Select(result => result.TemplatePackage.Identifier);
        }
        #endregion
    }
}

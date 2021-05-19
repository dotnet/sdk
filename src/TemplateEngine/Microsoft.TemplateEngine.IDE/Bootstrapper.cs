// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using ITemplateCreationResult = Microsoft.TemplateEngine.Edge.Template.ITemplateCreationResult;
using TemplateCreator = Microsoft.TemplateEngine.Edge.Template.TemplateCreator;

namespace Microsoft.TemplateEngine.IDE
{
    public class Bootstrapper : IDisposable
    {
        private readonly ITemplateEngineHost _host;
        private readonly TemplateCreator _templateCreator;
        private readonly Edge.Settings.TemplatePackageManager _templatePackagesManager;
        private readonly EngineEnvironmentSettings _engineEnvironmentSettings;

        /// <summary>
        /// Creates the instance.
        /// </summary>
        /// <param name="host">caller <see cref="ITemplateEngineHost"/>.</param>
        /// <param name="virtualizeConfiguration">if true, settings will be stored in memory and will be disposed with instance.</param>
        /// <param name="loadDefaultComponents">if true, the default components (providers, installers, generator) will be loaded. Same as calling <see cref="LoadDefaultComponents()"/> after instance is created.</param>
        public Bootstrapper(ITemplateEngineHost host, bool virtualizeConfiguration, bool loadDefaultComponents = true)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _engineEnvironmentSettings = new EngineEnvironmentSettings(host, virtualizeSettings: virtualizeConfiguration);
            _templateCreator = new TemplateCreator(_engineEnvironmentSettings);
            _templatePackagesManager = new Edge.Settings.TemplatePackageManager(_engineEnvironmentSettings);
            if (loadDefaultComponents)
            {
                LoadDefaultComponents();
            }
        }

        /// <summary>
        /// Loads default components: template package providers and installers defined in Microsoft.TemplateEngine.Edge and default template generator defined in Microsoft.TemplateEngine.Orchestrator.RunnableProjects.
        /// </summary>
        public void LoadDefaultComponents()
        {
            foreach ((Type Type, IIdentifiedComponent Instance) component in Orchestrator.RunnableProjects.Components.AllComponents)
            {
                AddComponent(component.Type, component.Instance);
            }
            foreach ((Type Type, IIdentifiedComponent Instance) component in Edge.Components.AllComponents)
            {
                AddComponent(component.Type, component.Instance);
            }
        }

        /// <summary>
        /// Adds component to manager, which can be looked up later via <see cref="IComponentManager.TryGetComponent{T}(Guid, out T)"/> or <see cref="IComponentManager.OfType{T}"/>.
        /// Added components are not persisted and need to be called every time new instance of <see cref="Bootstrapper"/> is created.
        /// </summary>
        /// <param name="interfaceType">Interface type that added component implements.</param>
        /// <param name="instance">Instance of type that implements <paramref name="interfaceType"/>.</param>
        public void AddComponent(Type interfaceType, IIdentifiedComponent component)
        {
            _engineEnvironmentSettings.Components.AddComponent(interfaceType, component);
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
            return _templatePackagesManager.GetTemplatesAsync(criteria, filters ?? Array.Empty<Func<ITemplateInfo, MatchInfo?>>(), cancellationToken);
        }

        /// <summary>
        /// Instantiates the template.
        /// </summary>
        /// <param name="info">The template to instantiate.</param>
        /// <param name="name">The name to use.</param>
        /// <param name="outputPath">The output directory for template instantiation.</param>
        /// <param name="parameters">The template parameters.</param>
        /// <param name="baselineName">The baseline configuration to use.</param>
        /// <returns><see cref="TemplateCreationResult"/> containing information on created template or error occurred.</returns>
#pragma warning disable RS0027 // Public API with optional parameter(s) should have the most parameters amongst its public overloads
        public Task<ITemplateCreationResult> CreateAsync(
            ITemplateInfo info,
            string? name,
            string outputPath,
            IReadOnlyDictionary<string, string?> parameters,
            string? baselineName = null,
            CancellationToken cancellationToken = default)
#pragma warning restore RS0027 // Public API with optional parameter(s) should have the most parameters amongst its public overloads
        {
            return _templateCreator.InstantiateAsync(
                info,
                name,
                fallbackName: null,
                outputPath: outputPath,
                inputParameters: parameters,
                forceCreation: false,
                baselineName: baselineName,
                dryRun: false,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Dry runs the template with given parameters.
        /// </summary>
        /// <param name="info">The template to instantiate.</param>
        /// <param name="name">The name to use.</param>
        /// <param name="outputPath">The output directory for template instantiation.</param>
        /// <param name="parameters">The template parameters.</param>
        /// <param name="baselineName">The baseline configuration to use.</param>
        /// <returns><see cref="ITemplateCreationResult"/> containing information on template that would be created or error occurred.</returns>
        public Task<ITemplateCreationResult> GetCreationEffectsAsync(
            ITemplateInfo info,
            string? name,
            string outputPath,
            IReadOnlyDictionary<string, string?> parameters,
            string? baselineName = null,
            CancellationToken cancellationToken = default)
        {
            return _templateCreator.InstantiateAsync(
                info,
                name,
                fallbackName: null,
                outputPath: outputPath,
                inputParameters: parameters,
                forceCreation: false,
                baselineName: baselineName,
                dryRun: true,
                cancellationToken: cancellationToken);
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
            return _templatePackagesManager.GetTemplatePackagesAsync(false, cancellationToken);
        }

        /// <summary>
        /// Gets the list of available managed template packages.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>the list of managed template packages.</returns>
        public Task<IReadOnlyList<IManagedTemplatePackage>> GetManagedTemplatePackages(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _templatePackagesManager.GetManagedTemplatePackagesAsync(false, cancellationToken);
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
                        managedPackageProvider = _templatePackagesManager.GetBuiltInManagedProvider(InstallationScope.Global);
                        break;
                    }
            }

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

            if (!managedPackages.Any())
            {
                return new List<UninstallResult>();
            }

            IEnumerable<IGrouping<IManagedTemplatePackageProvider, IManagedTemplatePackage>> requestsGroupedByProvider = managedPackages.GroupBy(package => package.ManagedProvider, package => package);
            IReadOnlyList<UninstallResult>[] uninstallResults = await Task.WhenAll(requestsGroupedByProvider.Select(packages => packages.Key.UninstallAsync(packages, cancellationToken))).ConfigureAwait(false);

            return uninstallResults.SelectMany(result => result).ToList();
        }

        #endregion Template Package Management

        public void Dispose() => _templatePackagesManager.Dispose();

        #region Obsolete

        [Obsolete("Use " + nameof(GetTemplatesAsync) + "instead")]
        public async Task<IReadOnlyCollection<Edge.Template.IFilteredTemplateInfo>> ListTemplates(bool exactMatchesOnly, params Func<ITemplateInfo, Edge.Template.MatchInfo?>[] filters)
        {
            return TemplateListFilter.FilterTemplates(await _templatePackagesManager.GetTemplatesAsync(default).ConfigureAwait(false), exactMatchesOnly, filters);
        }

        [Obsolete("Use ITemplateEngineHost.BuiltInComponents or AddComponent to add components.")]
        public void Register(Type type)
        {
            _engineEnvironmentSettings.Components.Register(type);
        }

        [Obsolete("Use ITemplateEngineHost.BuiltInComponents or AddComponent to add components.")]
        public void Register(Assembly assembly)
        {
            _engineEnvironmentSettings.Components.RegisterMany(assembly.GetTypes());
        }

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
            return uninstallTask.Result
                .Where(result => result.Success)
                .Select(result => result.TemplatePackage!.Identifier);
        }

        [Obsolete("Use Task<TemplateCreationResult> CreateAsync(ITemplateInfo info, string? name, string outputPath, IReadOnlyDictionary<string, string> parameters, string? baselineName = null, CancellationToken cancellationToken = default) instead.")]
        public async Task<ICreationResult?> CreateAsync(ITemplateInfo info, string name, string outputPath, IReadOnlyDictionary<string, string?> parameters, bool skipUpdateCheck, string baselineName)
        {
            ITemplateCreationResult instantiateResult = await _templateCreator.InstantiateAsync(info, name, name, outputPath, parameters, false, baselineName).ConfigureAwait(false);
            return instantiateResult.CreationResult;
        }

        [Obsolete("Use Task<TemplateCreationResult> GetCreationEffectsAsync(ITemplateInfo info, string? name, string outputPath, IReadOnlyDictionary<string, string> parameters, string? baselineName = null, CancellationToken cancellationToken = default) instead.")]
        public async Task<ICreationEffects?> GetCreationEffectsAsync(ITemplateInfo info, string name, string outputPath, IReadOnlyDictionary<string, string?> parameters, string baselineName)
        {
            ITemplateCreationResult instantiateResult = await _templateCreator.InstantiateAsync(info, name, name, outputPath, parameters,  false, baselineName, true).ConfigureAwait(false);
            return instantiateResult.CreationEffects;
        }

        #endregion Obsolete
    }
}

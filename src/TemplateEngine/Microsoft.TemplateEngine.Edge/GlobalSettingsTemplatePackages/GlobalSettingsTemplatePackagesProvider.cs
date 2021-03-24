// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.GlobalSettings;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackages;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge
{
    internal class GlobalSettingsTemplatePackagesProvider : IManagedTemplatePackagesProvider
    {
        private readonly string PackagesFolder;
        private IEngineEnvironmentSettings _environmentSettings;
        private Dictionary<Guid, IInstaller> _installersByGuid = new Dictionary<Guid, IInstaller>();
        private Dictionary<string, IInstaller> _installersByName = new Dictionary<string, IInstaller>();

        public GlobalSettingsTemplatePackagesProvider
            (GlobalSettingsTemplatePackagesProviderFactory factory, IEngineEnvironmentSettings settings)
        {
            _ = factory ?? throw new ArgumentNullException(nameof(factory));
            _ = settings ?? throw new ArgumentNullException(nameof(settings));

            Factory = factory;
            PackagesFolder = Path.Combine(settings.Paths.TemplateEngineRootDir, "packages");
            if (!settings.Host.FileSystem.DirectoryExists(PackagesFolder))
            {
                settings.Host.FileSystem.CreateDirectory(PackagesFolder);
            }

            _environmentSettings = settings;
            foreach (var installerFactory in settings.SettingsLoader.Components.OfType<IInstallerFactory>())
            {
                var installer = installerFactory.CreateInstaller(this, settings, PackagesFolder);
                _installersByName[installerFactory.Name] = installer;
                _installersByGuid[installerFactory.Id] = installer;
            }

            // We can't just add "SettingsChanged+=SourcesChanged", because SourcesChanged is null at this time.
            settings.SettingsLoader.GlobalSettings.SettingsChanged += () => SourcesChanged?.Invoke();
        }

        public event Action SourcesChanged;

        public ITemplatePackagesProviderFactory Factory { get; }

        public async Task<IReadOnlyList<ITemplatePackage>> GetAllSourcesAsync(CancellationToken cancellationToken)
        {
            var list = new List<ITemplatePackage>();
            foreach (TemplatePackageData entry in await _environmentSettings.SettingsLoader.GlobalSettings.GetInstalledTemplatesPackagesAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_installersByGuid.TryGetValue(entry.InstallerId, out var installer))
                {
                    list.Add(installer.Deserialize(this, entry));

                }
                else
                {
                    list.Add(new TemplatePackage(this, entry.MountPointUri, entry.LastChangeTime));
                }
            }
            return list;
        }

        public async Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionsAsync(IEnumerable<IManagedTemplatePackage> sources, CancellationToken cancellationToken)
        {
            _ = sources ?? throw new ArgumentNullException(nameof(sources));

            var tasks = new List<Task<IReadOnlyList<CheckUpdateResult>>>();
            foreach (var sourcesGroupedByInstaller in sources.GroupBy(s => s.Installer))
            {
                tasks.Add(sourcesGroupedByInstaller.Key.GetLatestVersionAsync(sourcesGroupedByInstaller, cancellationToken));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);

            var result = new List<CheckUpdateResult>();
            foreach (var task in tasks)
            {
                result.AddRange(task.Result);
            }
            return result;
        }

        public async Task<IReadOnlyList<InstallResult>> InstallAsync(IEnumerable<InstallRequest> installRequests, CancellationToken cancellationToken)
        {
            _ = installRequests ?? throw new ArgumentNullException(nameof(installRequests));
            if (!installRequests.Any())
            {
                return new List<InstallResult>();
            }

            using var disposable = await _environmentSettings.SettingsLoader.GlobalSettings.LockAsync(cancellationToken).ConfigureAwait(false);
            var packages = new List<TemplatePackageData>(await _environmentSettings.SettingsLoader.GlobalSettings.GetInstalledTemplatesPackagesAsync(cancellationToken).ConfigureAwait(false));
            var results = await Task.WhenAll(installRequests.Select(async installRequest =>
            {
                var installersThatCanInstall = new List<IInstaller>();
                foreach (var install in _installersByName.Values)
                {
                    if (await install.CanInstallAsync(installRequest, cancellationToken).ConfigureAwait(false))
                    {
                        installersThatCanInstall.Add(install);
                    }
                }
                if (installersThatCanInstall.Count == 0)
                {
                    return InstallResult.CreateFailure(installRequest, InstallerErrorCode.UnsupportedRequest, $"{installRequest.Identifier} cannot be installed");
                }

                IInstaller installer = installersThatCanInstall[0];
                return await InstallAsync(packages, installRequest, installer, cancellationToken).ConfigureAwait(false);
            })).ConfigureAwait(false);
            await _environmentSettings.SettingsLoader.GlobalSettings.SetInstalledTemplatesPackagesAsync(packages, cancellationToken).ConfigureAwait(false);
            return results;
        }

        public async Task<IReadOnlyList<UninstallResult>> UninstallAsync(IEnumerable<IManagedTemplatePackage> sources, CancellationToken cancellationToken)
        {
            _ = sources ?? throw new ArgumentNullException(nameof(sources));
            if (!sources.Any())
            {
                return new List<UninstallResult>();
            }

            using var disposable = await _environmentSettings.SettingsLoader.GlobalSettings.LockAsync(cancellationToken).ConfigureAwait(false);

            var packages = new List<TemplatePackageData>(await _environmentSettings.SettingsLoader.GlobalSettings.GetInstalledTemplatesPackagesAsync(cancellationToken).ConfigureAwait(false));
            var results = await Task.WhenAll(sources.Select(async source =>
             {
                 UninstallResult result = await source.Installer.UninstallAsync(source, cancellationToken).ConfigureAwait(false);
                 if (result.Success)
                 {
                     lock (packages)
                     {
                         packages.RemoveAll(p => p.MountPointUri == source.MountPointUri);
                     }
                 }
                 return result;
             })).ConfigureAwait(false);
            await _environmentSettings.SettingsLoader.GlobalSettings.SetInstalledTemplatesPackagesAsync(packages, cancellationToken).ConfigureAwait(false);
            return results;

        }

        public async Task<IReadOnlyList<UpdateResult>> UpdateAsync(IEnumerable<UpdateRequest> updateRequests, CancellationToken cancellationToken)
        {
            _ = updateRequests ?? throw new ArgumentNullException(nameof(updateRequests));
            IEnumerable<UpdateRequest> updatesToApply = updateRequests.Where(request => request.Version != request.Source.Version);

            using var disposable = await _environmentSettings.SettingsLoader.GlobalSettings.LockAsync(cancellationToken).ConfigureAwait(false);

            var packages = new List<TemplatePackageData>(await _environmentSettings.SettingsLoader.GlobalSettings.GetInstalledTemplatesPackagesAsync(cancellationToken).ConfigureAwait(false));
            var results = await Task.WhenAll(updatesToApply.Select(updateRequest => UpdateAsync(packages, updateRequest, cancellationToken))).ConfigureAwait(false);
            await _environmentSettings.SettingsLoader.GlobalSettings.SetInstalledTemplatesPackagesAsync(packages, cancellationToken).ConfigureAwait(false);
            return results;

        }

        private async Task<UpdateResult> UpdateAsync(List<TemplatePackageData> packages, UpdateRequest updateRequest, CancellationToken cancellationToken)
        {
            (InstallerErrorCode result, string message) = await EnsureInstallPrerequisites(packages, updateRequest.Source.Identifier, updateRequest.Version, updateRequest.Source.Installer, cancellationToken, update: true).ConfigureAwait(false);
            if (result != InstallerErrorCode.Success)
            {
                return UpdateResult.CreateFailure(updateRequest, result, message);
            }

            UpdateResult updateResult = await updateRequest.Source.Installer.UpdateAsync(updateRequest, cancellationToken).ConfigureAwait(false);
            if (!updateResult.Success)
            {
                return updateResult;
            }
            lock (packages)
            {
                packages.Add(updateRequest.Source.Installer.Serialize(updateResult.Source));
            }
            return updateResult;
        }

        private async Task<(InstallerErrorCode, string)> EnsureInstallPrerequisites(List<TemplatePackageData> packages, string identifier, string version, IInstaller installer, CancellationToken cancellationToken, bool update = false)
        {
            var sources = await GetAllSourcesAsync(cancellationToken).ConfigureAwait(false);

            //check if the source with same identifier is already installed
            if (sources.OfType<IManagedTemplatePackage>().FirstOrDefault(s => s.Identifier == identifier && s.Installer == installer) is IManagedTemplatePackage sourceToBeUpdated)
            {
                //if same version is already installed - return
                if (sourceToBeUpdated.Version == version)
                {
                    return (InstallerErrorCode.AlreadyInstalled, $"{sourceToBeUpdated.DisplayName} is already installed.");
                }
                if (!update)
                {
                    _environmentSettings.Host.LogMessage(
                        string.Format(
                            LocalizableStrings.GlobalSettingsTemplatePackagesProvider_Info_PackageAlreadyInstalled,
                            sourceToBeUpdated.Identifier,
                            sourceToBeUpdated.Version,
                            string.IsNullOrWhiteSpace(identifier) ?
                                LocalizableStrings.Generic_LatestVersion :
                                string.Format(LocalizableStrings.Generic_Version, version)));
                }
                //if different version is installed - uninstall previous version first
                UninstallResult uninstallResult = await installer.UninstallAsync(sourceToBeUpdated, cancellationToken).ConfigureAwait(false);
                if (!uninstallResult.Success)
                {
                    return (InstallerErrorCode.UpdateUninstallFailed, uninstallResult.ErrorMessage);
                }
                _environmentSettings.Host.LogMessage(
                    string.Format(
                        LocalizableStrings.GlobalSettingsTemplatePackagesProvider_Info_PackageUninstalled,
                        sourceToBeUpdated.DisplayName));

                lock (packages)
                {
                    packages.RemoveAll(p => p.MountPointUri == sourceToBeUpdated.MountPointUri);
                }
            }
            return (InstallerErrorCode.Success, string.Empty);
        }

        private async Task<InstallResult> InstallAsync(List<TemplatePackageData> packages, InstallRequest installRequest, IInstaller installer, CancellationToken cancellationToken)
        {
            _ = installRequest ?? throw new ArgumentNullException(nameof(installRequest));
            _ = installer ?? throw new ArgumentNullException(nameof(installer));

            (InstallerErrorCode result, string message) = await EnsureInstallPrerequisites(packages, installRequest.Identifier, installRequest.Version, installer, cancellationToken).ConfigureAwait(false);
            if (result != InstallerErrorCode.Success)
            {
                return InstallResult.CreateFailure(installRequest, result, message);
            }

            InstallResult installResult = await installer.InstallAsync(installRequest, cancellationToken).ConfigureAwait(false);
            if (!installResult.Success)
            {
                return installResult;
            }
            lock (packages)
            {
                packages.Add(installer.Serialize(installResult.Source));
            }
            return installResult;
        }
    }
}

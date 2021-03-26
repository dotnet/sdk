// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class NuGetInstaller : IInstaller, ISerializableInstaller
    {
        private const string DebugLogCategory = "Installer";
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly string _installPath;
        private readonly IDownloader _packageDownloader;
        private readonly IUpdateChecker _updateChecker;

        public NuGetInstaller(IInstallerFactory factory, IManagedTemplatePackageProvider provider, IEngineEnvironmentSettings settings, string installPath) 
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _environmentSettings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrWhiteSpace(installPath))
            {
                throw new ArgumentException($"{nameof(installPath)} should not be null or empty", nameof(installPath));
            }
            if (!_environmentSettings.Host.FileSystem.DirectoryExists(installPath))
            {
                _environmentSettings.Host.FileSystem.CreateDirectory(installPath);
            }
            _installPath = installPath;

            NuGetApiPackageManager packageManager = new NuGetApiPackageManager(settings);
            _packageDownloader = packageManager;
            _updateChecker = packageManager;
        }

        public NuGetInstaller(IInstallerFactory factory, IManagedTemplatePackageProvider provider, IEngineEnvironmentSettings settings, string installPath, IDownloader packageDownloader, IUpdateChecker updateChecker)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _environmentSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            _packageDownloader = packageDownloader ?? throw new ArgumentNullException(nameof(packageDownloader));
            _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));

            if (string.IsNullOrWhiteSpace(installPath))
            {
                throw new ArgumentException($"{nameof(installPath)} should not be null or empty", nameof(installPath));
            }
            if (!_environmentSettings.Host.FileSystem.DirectoryExists(installPath))
            {
                _environmentSettings.Host.FileSystem.CreateDirectory(installPath);
            }
            _installPath = installPath;
        }

        public IInstallerFactory Factory { get; }

        public IManagedTemplatePackageProvider Provider { get; }

        public Task<bool> CanInstallAsync(InstallRequest installationRequest, CancellationToken cancellationToken)
        {
            try
            {
                ReadPackageInformation(installationRequest.Identifier);
            }
            catch (Exception)
            {
                _environmentSettings.Host.LogDiagnosticMessage($"{installationRequest.Identifier} is not a local NuGet package.", DebugLogCategory);

                //check if identifier is a valid package ID
                bool validPackageId = PackageIdValidator.IsValidPackageId(installationRequest.Identifier);
                //check if version is specified it is correct version
                bool hasValidVersion = string.IsNullOrWhiteSpace(installationRequest.Version) || NuGetVersion.TryParse(installationRequest.Version, out _);
                if (!validPackageId)
                {
                    _environmentSettings.Host.LogDiagnosticMessage($"{installationRequest.Identifier} is not a valid NuGet package ID.", DebugLogCategory);
                }
                if (!hasValidVersion)
                {
                    _environmentSettings.Host.LogDiagnosticMessage($"{installationRequest.Version} is not a valid NuGet package version.", DebugLogCategory);
                }
                if (validPackageId && hasValidVersion)
                {
                    _environmentSettings.Host.LogDiagnosticMessage($"{installationRequest.Version} is identified as the downloadable NuGet package.", DebugLogCategory);
                }

                //not a local package file
                return Task.FromResult(validPackageId && hasValidVersion);
            }
            _environmentSettings.Host.LogDiagnosticMessage($"{installationRequest.Identifier} is identified as the local NuGet package.", DebugLogCategory);
            return Task.FromResult(true);
        }

        public IManagedTemplatePackage Deserialize(IManagedTemplatePackageProvider provider, TemplatePackageData data)
        {
            return new NuGetManagedTemplatePackage(_environmentSettings, this, data.MountPointUri, data.Details);
        }

        public async Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionAsync(IEnumerable<IManagedTemplatePackage> sources, CancellationToken cancellationToken)
        {
            _ = sources ?? throw new ArgumentNullException(nameof(sources));
            return await Task.WhenAll(sources.Select(async source =>
                {
                    if (source is NuGetManagedTemplatePackage nugetSource)
                    {
                        try
                        {
                            (string latestVersion, bool isLatestVersion) = await _updateChecker.GetLatestVersionAsync(nugetSource.Identifier, nugetSource.Version, nugetSource.NuGetSource, cancellationToken).ConfigureAwait(false);
                            return CheckUpdateResult.CreateSuccess(source, latestVersion, isLatestVersion);
                        }
                        catch (PackageNotFoundException e)
                        {
                            return CheckUpdateResult.CreateFailure(source, InstallerErrorCode.PackageNotFound, e.Message);
                        }
                        catch (InvalidNuGetSourceException e)
                        {
                            return CheckUpdateResult.CreateFailure(source, InstallerErrorCode.InvalidSource, e.Message);
                        }
                        catch (Exception e)
                        {
                            _environmentSettings.Host.LogDiagnosticMessage($"Retreving latest version for package {source.DisplayName} failed.", DebugLogCategory);
                            _environmentSettings.Host.LogDiagnosticMessage($"Details:{e.ToString()}", DebugLogCategory);
                            return CheckUpdateResult.CreateFailure(source, InstallerErrorCode.GenericError, $"Failed to check the update for the package {source.Identifier}, reason: {e.Message}");
                        }
                    }
                    else
                    {
                        return CheckUpdateResult.CreateFailure(source, InstallerErrorCode.UnsupportedRequest, $"source {source.Identifier} is not supported by installer {Factory.Name}");
                    }
                })).ConfigureAwait(false);
        }

        public async Task<InstallResult> InstallAsync(InstallRequest installRequest, CancellationToken cancellationToken)
        {
            _ = installRequest ?? throw new ArgumentNullException(nameof(installRequest));

            if (!await CanInstallAsync(installRequest, cancellationToken).ConfigureAwait(false))
            {
                return InstallResult.CreateFailure(installRequest, InstallerErrorCode.UnsupportedRequest, $"The install request {installRequest} cannot be processed by installer {Factory.Name}");
            }

            try
            {
                Dictionary<string, string> sourceDetails = new Dictionary<string, string>();
                NuGetPackageInfo nuGetPackageInfo;
                if (IsLocalPackage(installRequest))
                {
                    sourceDetails[NuGetManagedTemplatePackage.LocalPackageKey] = true.ToString();
                    nuGetPackageInfo = InstallLocalPackage(installRequest);
                }
                else
                {
                    string[] additionalNuGetSources = Array.Empty<string>();
                    if (installRequest.Details != null && installRequest.Details.TryGetValue(InstallerConstants.NuGetSourcesKey, out string nugetSources))
                    {
                        additionalNuGetSources = nugetSources.Split(InstallerConstants.NuGetSourcesSeparator);
                    }

                    nuGetPackageInfo = await _packageDownloader.DownloadPackageAsync(
                        _installPath,
                        installRequest.Identifier,
                        installRequest.Version,
                        additionalNuGetSources,
                        cancellationToken)
                        .ConfigureAwait(false);
                }

                sourceDetails[NuGetManagedTemplatePackage.AuthorKey] = nuGetPackageInfo.Author;
                sourceDetails[NuGetManagedTemplatePackage.NuGetSourceKey] = nuGetPackageInfo.NuGetSource;
                sourceDetails[NuGetManagedTemplatePackage.PackageIdKey] = nuGetPackageInfo.PackageIdentifier;
                sourceDetails[NuGetManagedTemplatePackage.PackageVersionKey] = nuGetPackageInfo.PackageVersion.ToString();
                NuGetManagedTemplatePackage source = new NuGetManagedTemplatePackage(_environmentSettings, this, nuGetPackageInfo.FullPath, sourceDetails);
                return InstallResult.CreateSuccess(installRequest, source);
            }
            catch (DownloadException e)
            {
                return InstallResult.CreateFailure(installRequest, InstallerErrorCode.DownloadFailed, e.Message);
            }
            catch (PackageNotFoundException e)
            {
                return InstallResult.CreateFailure(installRequest, InstallerErrorCode.PackageNotFound, e.Message);
            }
            catch (InvalidNuGetSourceException e)
            {
                return InstallResult.CreateFailure(installRequest, InstallerErrorCode.InvalidSource, e.Message);
            }
            catch (InvalidNuGetPackageException e)
            {
                return InstallResult.CreateFailure(installRequest, InstallerErrorCode.InvalidPackage, e.Message);
            }
            catch (Exception e)
            {
                _environmentSettings.Host.LogDiagnosticMessage($"Installing {installRequest.DisplayName} failed.", DebugLogCategory);
                _environmentSettings.Host.LogDiagnosticMessage($"Details:{e.ToString()}", DebugLogCategory);
                return InstallResult.CreateFailure(installRequest, InstallerErrorCode.GenericError, $"Failed to install the package {installRequest.DisplayName}, reason: {e.Message}");
            }
        }

        public TemplatePackageData Serialize(IManagedTemplatePackage managedSource)
        {
            _ = managedSource ?? throw new ArgumentNullException(nameof(managedSource));
            if (!(managedSource is NuGetManagedTemplatePackage nuGetTemplatePackage))
            {
                return new TemplatePackageData()
                {
                    InstallerId = Factory.Id,
                    MountPointUri = managedSource.MountPointUri,
                    LastChangeTime = default
                };
            }

            return new TemplatePackageData()
            {
                InstallerId = Factory.Id,
                MountPointUri = nuGetTemplatePackage.MountPointUri,
                LastChangeTime = nuGetTemplatePackage.LastChangeTime,
                Details = nuGetTemplatePackage.Details
            };
        }

        public Task<UninstallResult> UninstallAsync(IManagedTemplatePackage managedSource, CancellationToken cancellationToken)
        {
            _ = managedSource ?? throw new ArgumentNullException(nameof(managedSource));
            if (!(managedSource is NuGetManagedTemplatePackage))
            {
                return Task.FromResult(UninstallResult.CreateFailure(managedSource, InstallerErrorCode.UnsupportedRequest, $"{managedSource.Identifier} is not supported by {Factory.Name}"));
            }
            try
            {
                _environmentSettings.Host.FileSystem.FileDelete(managedSource.MountPointUri);
                return Task.FromResult(UninstallResult.CreateSuccess(managedSource));
            }
            catch (Exception ex)
            {
                _environmentSettings.Host.LogDiagnosticMessage($"Uninstalling {managedSource.DisplayName} failed.", DebugLogCategory);
                _environmentSettings.Host.LogDiagnosticMessage($"Details:{ex.ToString()}", DebugLogCategory);
                return Task.FromResult(UninstallResult.CreateFailure(managedSource, InstallerErrorCode.GenericError, $"Failed to uninstall {managedSource.DisplayName}, reason: {ex.Message}"));
            }
        }

        public async Task<UpdateResult> UpdateAsync(UpdateRequest updateRequest, CancellationToken cancellationToken)
        {
            _ = updateRequest ?? throw new ArgumentNullException(nameof(updateRequest));
            if (string.IsNullOrWhiteSpace(updateRequest.Version))
            {
                throw new ArgumentException("Version cannot be null or empty", nameof(updateRequest.Version));
            }

            //ensure uninstall is performed
            UninstallResult uninstallResult = await UninstallAsync(updateRequest.TemplatePackage, cancellationToken).ConfigureAwait(false);
            if (!uninstallResult.Success)
            {
                return UpdateResult.CreateFailure(updateRequest, uninstallResult.Error, uninstallResult.ErrorMessage);
            }

            var nuGetManagedSource = updateRequest.TemplatePackage as NuGetManagedTemplatePackage;
            Dictionary<string, string> installationDetails = new Dictionary<string, string>();
            if (nuGetManagedSource != null && !string.IsNullOrWhiteSpace(nuGetManagedSource.NuGetSource))
            {
                installationDetails.Add(InstallerConstants.NuGetSourcesKey, nuGetManagedSource.NuGetSource);
            }
            InstallRequest installRequest = new InstallRequest(updateRequest.TemplatePackage.Identifier, updateRequest.Version, details: installationDetails);
            return UpdateResult.FromInstallResult(updateRequest, await InstallAsync(installRequest, cancellationToken).ConfigureAwait(false));
        }

        private bool IsLocalPackage(InstallRequest installRequest)
        {
            return _environmentSettings.Host.FileSystem.FileExists(installRequest.Identifier);
        }

        private NuGetPackageInfo InstallLocalPackage(InstallRequest installRequest)
        {
            _ = installRequest ?? throw new ArgumentNullException(nameof(installRequest));

            NuGetPackageInfo packageInfo;
            try
            {
                packageInfo = ReadPackageInformation(installRequest.Identifier);
            }
            catch (Exception ex)
            {
                _environmentSettings.Host.OnCriticalError(null, string.Format(LocalizableStrings.NuGetInstaller_Error_FailedToReadPackage, installRequest.Identifier), null, 0);
                _environmentSettings.Host.LogDiagnosticMessage(DebugLogCategory, $"Details: {ex.ToString()}.");
                throw new InvalidNuGetPackageException(installRequest.Identifier, ex);
            }
            string targetPackageLocation = Path.Combine(_installPath, packageInfo.PackageIdentifier + "." + packageInfo.PackageVersion + ".nupkg");
            if (_environmentSettings.Host.FileSystem.FileExists(targetPackageLocation))
            {
                _environmentSettings.Host.OnCriticalError(null, string.Format(LocalizableStrings.NuGetInstaller_Error_CopyFailed, installRequest.Identifier, targetPackageLocation), null, 0);
                _environmentSettings.Host.OnCriticalError(null, string.Format(LocalizableStrings.NuGetInstaller_Error_FileAlreadyExists, targetPackageLocation), null, 0);
                throw new DownloadException(packageInfo.PackageIdentifier, packageInfo.PackageVersion, installRequest.Identifier);
            }

            try
            {
                _environmentSettings.Host.FileSystem.FileCopy(installRequest.Identifier, targetPackageLocation, overwrite: false);
                packageInfo.FullPath = targetPackageLocation;
            }
            catch (Exception ex)
            {
                _environmentSettings.Host.OnCriticalError(null, string.Format(LocalizableStrings.NuGetInstaller_Error_CopyFailed, installRequest.Identifier, targetPackageLocation), null, 0);
                _environmentSettings.Host.LogDiagnosticMessage(DebugLogCategory, $"Details: {ex.ToString()}.");
                throw new DownloadException(packageInfo.PackageIdentifier, packageInfo.PackageVersion, installRequest.Identifier);
            }
            return packageInfo;
        }

        private NuGetPackageInfo ReadPackageInformation(string packageLocation)
        {
            using Stream inputStream = _environmentSettings.Host.FileSystem.OpenRead(packageLocation);
            using PackageArchiveReader reader = new PackageArchiveReader(inputStream);

            NuspecReader nuspec = reader.NuspecReader;

            return new NuGetPackageInfo
            {
                FullPath = packageLocation,
                Author = nuspec.GetAuthors(),
                PackageIdentifier = nuspec.GetId(),
                PackageVersion = nuspec.GetVersion().ToNormalizedString()
            };
        }
    }
}

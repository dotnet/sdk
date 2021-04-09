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

        public NuGetInstaller(IInstallerFactory factory, IEngineEnvironmentSettings settings, string installPath)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
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

        public NuGetInstaller(IInstallerFactory factory, IEngineEnvironmentSettings settings, string installPath, IDownloader packageDownloader, IUpdateChecker updateChecker)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
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

        public Task<bool> CanInstallAsync(InstallRequest installationRequest, CancellationToken cancellationToken)
        {
            try
            {
                ReadPackageInformation(installationRequest.PackageIdentifier);
            }
            catch (Exception)
            {
                _environmentSettings.Host.LogDiagnosticMessage($"{installationRequest.PackageIdentifier} is not a local NuGet package.", DebugLogCategory);

                //check if identifier is a valid package ID
                bool validPackageId = PackageIdValidator.IsValidPackageId(installationRequest.PackageIdentifier);
                //check if version is specified it is correct version
                bool hasValidVersion = string.IsNullOrWhiteSpace(installationRequest.Version) || NuGetVersion.TryParse(installationRequest.Version, out _);
                if (!validPackageId)
                {
                    _environmentSettings.Host.LogDiagnosticMessage($"{installationRequest.PackageIdentifier} is not a valid NuGet package ID.", DebugLogCategory);
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
            _environmentSettings.Host.LogDiagnosticMessage($"{installationRequest.PackageIdentifier} is identified as the local NuGet package.", DebugLogCategory);
            return Task.FromResult(true);
        }

        public IManagedTemplatePackage Deserialize(IManagedTemplatePackageProvider provider, TemplatePackageData data)
        {
            _ = provider ?? throw new ArgumentNullException(nameof(provider));
            if (data.InstallerId != Factory.Id)
            {
                throw new ArgumentException($"{nameof(NuGetInstaller)} can only deserialize packages with {nameof(data.InstallerId)} {Factory.Id}", nameof(data));
            }
            return NuGetManagedTemplatePackage.Deserialize(_environmentSettings, this, provider, data.MountPointUri, data.Details);
        }

        public async Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionAsync(IEnumerable<IManagedTemplatePackage> packages, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = packages ?? throw new ArgumentNullException(nameof(packages));
            return await Task.WhenAll(packages.Select(async package =>
                {
                    if (package is NuGetManagedTemplatePackage nugetPackage)
                    {
                        try
                        {
                            (string latestVersion, bool isLatestVersion) = await _updateChecker.GetLatestVersionAsync(nugetPackage.Identifier, nugetPackage.Version, nugetPackage.NuGetSource, cancellationToken).ConfigureAwait(false);
                            return CheckUpdateResult.CreateSuccess(package, latestVersion, isLatestVersion);
                        }
                        catch (PackageNotFoundException e)
                        {
                            return CheckUpdateResult.CreateFailure(package, InstallerErrorCode.PackageNotFound, e.Message);
                        }
                        catch (InvalidNuGetSourceException e)
                        {
                            return CheckUpdateResult.CreateFailure(package, InstallerErrorCode.InvalidSource, e.Message);
                        }
                        catch (OperationCanceledException)
                        {
                            return CheckUpdateResult.CreateFailure(package, InstallerErrorCode.GenericError, "Operation canceled");
                        }
                        catch (Exception e)
                        {
                            _environmentSettings.Host.LogDiagnosticMessage($"Retrieving latest version for package {package.DisplayName} failed.", DebugLogCategory);
                            _environmentSettings.Host.LogDiagnosticMessage($"Details:{e}", DebugLogCategory);
                            return CheckUpdateResult.CreateFailure(package, InstallerErrorCode.GenericError, $"Failed to check the update for the package {package.Identifier}, reason: {e.Message}");
                        }
                    }
                    else
                    {
                        return CheckUpdateResult.CreateFailure(package, InstallerErrorCode.UnsupportedRequest, $"package {package.Identifier} is not supported by installer {Factory.Name}");
                    }
                })).ConfigureAwait(false);
        }

        public async Task<InstallResult> InstallAsync(InstallRequest installRequest, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = installRequest ?? throw new ArgumentNullException(nameof(installRequest));
            _ = provider ?? throw new ArgumentNullException(nameof(provider));

            if (!await CanInstallAsync(installRequest, cancellationToken).ConfigureAwait(false))
            {
                return InstallResult.CreateFailure(installRequest, InstallerErrorCode.UnsupportedRequest, $"The install request {installRequest} cannot be processed by installer {Factory.Name}");
            }

            try
            {
                bool isLocalPackage = IsLocalPackage(installRequest);
                NuGetPackageInfo nuGetPackageInfo;
                if (isLocalPackage)
                {
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
                        installRequest.PackageIdentifier,
                        installRequest.Version,
                        additionalNuGetSources,
                        cancellationToken)
                        .ConfigureAwait(false);
                }

                NuGetManagedTemplatePackage package = new NuGetManagedTemplatePackage(
                    _environmentSettings,
                    installer: this,
                    provider,
                    nuGetPackageInfo.FullPath,
                    nuGetPackageInfo.PackageIdentifier)
                {
                    Author = nuGetPackageInfo.Author,
                    NuGetSource = nuGetPackageInfo.NuGetSource,
                    Version = nuGetPackageInfo.PackageVersion.ToString(),
                    LocalPackage = isLocalPackage
                };

                return InstallResult.CreateSuccess(installRequest, package);
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

        public TemplatePackageData Serialize(IManagedTemplatePackage templatePackage)
        {
            _ = templatePackage ?? throw new ArgumentNullException(nameof(templatePackage));
            NuGetManagedTemplatePackage nuGetTemplatePackage = templatePackage as NuGetManagedTemplatePackage
                ?? throw new ArgumentException($"{nameof(templatePackage)} should be of type {nameof(NuGetManagedTemplatePackage)}", nameof(templatePackage));

            return new TemplatePackageData(
                Factory.Id,
                nuGetTemplatePackage.MountPointUri,
                nuGetTemplatePackage.LastChangeTime,
                nuGetTemplatePackage.Details);
        }

        public Task<UninstallResult> UninstallAsync(IManagedTemplatePackage templatePackage, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = templatePackage ?? throw new ArgumentNullException(nameof(templatePackage));
            if (!(templatePackage is NuGetManagedTemplatePackage))
            {
                return Task.FromResult(UninstallResult.CreateFailure(templatePackage, InstallerErrorCode.UnsupportedRequest, $"{templatePackage.Identifier} is not supported by {Factory.Name}"));
            }
            try
            {
                _environmentSettings.Host.FileSystem.FileDelete(templatePackage.MountPointUri);
                return Task.FromResult(UninstallResult.CreateSuccess(templatePackage));
            }
            catch (Exception ex)
            {
                _environmentSettings.Host.LogDiagnosticMessage($"Uninstalling {templatePackage.DisplayName} failed.", DebugLogCategory);
                _environmentSettings.Host.LogDiagnosticMessage($"Details:{ex.ToString()}", DebugLogCategory);
                return Task.FromResult(UninstallResult.CreateFailure(templatePackage, InstallerErrorCode.GenericError, $"Failed to uninstall {templatePackage.DisplayName}, reason: {ex.Message}"));
            }
        }

        public async Task<UpdateResult> UpdateAsync(UpdateRequest updateRequest, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = updateRequest ?? throw new ArgumentNullException(nameof(updateRequest));
            _ = provider ?? throw new ArgumentNullException(nameof(provider));

            if (string.IsNullOrWhiteSpace(updateRequest.Version))
            {
                throw new ArgumentException("Version cannot be null or empty", nameof(updateRequest.Version));
            }

            //ensure uninstall is performed
            UninstallResult uninstallResult = await UninstallAsync(updateRequest.TemplatePackage, provider, cancellationToken).ConfigureAwait(false);
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
            return UpdateResult.FromInstallResult(updateRequest, await InstallAsync(installRequest, provider, cancellationToken).ConfigureAwait(false));
        }

        private bool IsLocalPackage(InstallRequest installRequest)
        {
            return _environmentSettings.Host.FileSystem.FileExists(installRequest.PackageIdentifier);
        }

        private NuGetPackageInfo InstallLocalPackage(InstallRequest installRequest)
        {
            _ = installRequest ?? throw new ArgumentNullException(nameof(installRequest));

            NuGetPackageInfo packageInfo;
            try
            {
                packageInfo = ReadPackageInformation(installRequest.PackageIdentifier);
            }
            catch (Exception ex)
            {
                _environmentSettings.Host.OnCriticalError(null, string.Format(LocalizableStrings.NuGetInstaller_Error_FailedToReadPackage, installRequest.PackageIdentifier), null, 0);
                _environmentSettings.Host.LogDiagnosticMessage($"Details: {ex.ToString()}.", DebugLogCategory);
                throw new InvalidNuGetPackageException(installRequest.PackageIdentifier, ex);
            }
            string targetPackageLocation = Path.Combine(_installPath, packageInfo.PackageIdentifier + "." + packageInfo.PackageVersion + ".nupkg");
            if (_environmentSettings.Host.FileSystem.FileExists(targetPackageLocation))
            {
                _environmentSettings.Host.OnCriticalError(null, string.Format(LocalizableStrings.NuGetInstaller_Error_CopyFailed, installRequest.PackageIdentifier, targetPackageLocation), null, 0);
                _environmentSettings.Host.OnCriticalError(null, string.Format(LocalizableStrings.NuGetInstaller_Error_FileAlreadyExists, targetPackageLocation), null, 0);
                throw new DownloadException(packageInfo.PackageIdentifier, packageInfo.PackageVersion, installRequest.PackageIdentifier);
            }

            try
            {
                _environmentSettings.Host.FileSystem.FileCopy(installRequest.PackageIdentifier, targetPackageLocation, overwrite: false);
                packageInfo = packageInfo.WithFullPath(targetPackageLocation);
            }
            catch (Exception ex)
            {
                _environmentSettings.Host.OnCriticalError(null, string.Format(LocalizableStrings.NuGetInstaller_Error_CopyFailed, installRequest.PackageIdentifier, targetPackageLocation), null, 0);
                _environmentSettings.Host.LogDiagnosticMessage($"Details: {ex.ToString()}.", DebugLogCategory);
                throw new DownloadException(packageInfo.PackageIdentifier, packageInfo.PackageVersion, installRequest.PackageIdentifier);
            }
            return packageInfo;
        }

        private NuGetPackageInfo ReadPackageInformation(string packageLocation)
        {
            using Stream inputStream = _environmentSettings.Host.FileSystem.OpenRead(packageLocation);
            using PackageArchiveReader reader = new PackageArchiveReader(inputStream);

            NuspecReader nuspec = reader.NuspecReader;

            return new NuGetPackageInfo(nuspec.GetAuthors(),
                                        packageLocation,
                                        null,
                                        nuspec.GetId(),
                                        nuspec.GetVersion().ToNormalizedString());
        }
    }
}

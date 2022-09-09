// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using NuGet.Packaging;

namespace Microsoft.TemplateEngine.Edge.Installers.NuGet
{
    internal class NuGetInstaller : IInstaller, ISerializableInstaller
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly ILogger _logger;
        private readonly string _installPath;
        private readonly IDownloader _packageDownloader;
        private readonly IUpdateChecker _updateChecker;

        public NuGetInstaller(IInstallerFactory factory, IEngineEnvironmentSettings settings, string installPath)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _environmentSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = settings.Host.LoggerFactory.CreateLogger<NuGetInstaller>();

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
            _logger = settings.Host.LoggerFactory.CreateLogger<NuGetInstaller>();
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
                _logger.LogDebug($"{installationRequest.PackageIdentifier} is not a local NuGet package.");

                //check if identifier is a valid package ID
                bool validPackageId = PackageIdValidator.IsValidPackageId(installationRequest.PackageIdentifier);
                //check if version is specified it is correct version
                bool hasValidVersion = NuGetVersionHelper.IsSupportedVersionString(installationRequest.Version);
                if (!validPackageId)
                {
                    _logger.LogDebug($"{installationRequest.PackageIdentifier} is not a valid NuGet package ID.");
                }
                if (!hasValidVersion)
                {
                    _logger.LogDebug($"{installationRequest.Version} is not a valid NuGet package version.");
                }
                if (validPackageId && hasValidVersion)
                {
                    _logger.LogDebug($"{installationRequest.DisplayName} is identified as the downloadable NuGet package.");
                }

                //not a local package file
                return Task.FromResult(validPackageId && hasValidVersion);
            }
            _logger.LogDebug($"{installationRequest.PackageIdentifier} is identified as the local NuGet package.");
            return Task.FromResult(true);
        }

        public IManagedTemplatePackage Deserialize(IManagedTemplatePackageProvider provider, TemplatePackageData data)
        {
            _ = provider ?? throw new ArgumentNullException(nameof(provider));
            if (data.InstallerId != Factory.Id)
            {
                throw new ArgumentException($"{nameof(NuGetInstaller)} can only deserialize packages with {nameof(data.InstallerId)} {Factory.Id}", nameof(data));
            }
            _ = data.Details ?? throw new ArgumentException($"{nameof(data)} should contain {nameof(data.Details)} with package identifier.", nameof(data));
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
                            return CheckUpdateResult.CreateFailure(
                                package,
                                InstallerErrorCode.PackageNotFound,
                                string.Format(LocalizableStrings.NuGetInstaller_Error_FailedToReadPackage, e.PackageIdentifier, string.Join(", ", e.SourcesList)));
                        }
                        catch (InvalidNuGetSourceException e)
                        {
                            string message = e.SourcesList == null || !e.SourcesList.Any()
                                ? LocalizableStrings.NuGetInstaller_InstallResut_Error_InvalidSources_None
                                : string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_InvalidSources, string.Join(", ", e.SourcesList));

                            return CheckUpdateResult.CreateFailure(
                                package,
                                InstallerErrorCode.InvalidSource,
                                message);
                        }
                        catch (OperationCanceledException)
                        {
                            return CheckUpdateResult.CreateFailure(
                                package,
                                InstallerErrorCode.GenericError,
                                LocalizableStrings.NuGetInstaller_InstallResut_Error_OperationCancelled);
                        }
                        catch (Exception e)
                        {
                            _logger.LogDebug($"Retrieving latest version for package {package.DisplayName} failed. Details: {e}.");
                            return CheckUpdateResult.CreateFailure(
                                package,
                                InstallerErrorCode.GenericError,
                                string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_UpdateCheckGeneric, package.DisplayName, e.Message));
                        }
                    }
                    else
                    {
                        return CheckUpdateResult.CreateFailure(
                            package,
                            InstallerErrorCode.UnsupportedRequest,
                            string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_PackageNotSupported, package.DisplayName, Factory.Name));
                    }
                })).ConfigureAwait(false);
        }

        public async Task<InstallResult> InstallAsync(InstallRequest installRequest, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = installRequest ?? throw new ArgumentNullException(nameof(installRequest));
            _ = provider ?? throw new ArgumentNullException(nameof(provider));

            if (!await CanInstallAsync(installRequest, cancellationToken).ConfigureAwait(false))
            {
                return InstallResult.CreateFailure(
                    installRequest,
                    InstallerErrorCode.UnsupportedRequest,
                    string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_PackageNotSupported, installRequest.DisplayName, Factory.Name));
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
                        force: installRequest.Force,
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
                    IsLocalPackage = isLocalPackage
                };

                return InstallResult.CreateSuccess(installRequest, package);
            }
            catch (DownloadException e)
            {
                string? packageLocation = e.SourcesList == null
                    ? e.PackageLocation
                    : string.Join(", ", e.SourcesList);

                return InstallResult.CreateFailure(
                    installRequest,
                    InstallerErrorCode.DownloadFailed,
                    string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_DownloadFailed, installRequest.DisplayName, packageLocation));
            }
            catch (PackageNotFoundException e)
            {
                return InstallResult.CreateFailure(
                    installRequest,
                    InstallerErrorCode.PackageNotFound,
                    string.Format(LocalizableStrings.NuGetInstaller_Error_FailedToReadPackage, e.PackageIdentifier, string.Join(", ", e.SourcesList)));
            }
            catch (InvalidNuGetSourceException e)
            {
                string message = e.SourcesList == null || !e.SourcesList.Any()
                    ? LocalizableStrings.NuGetInstaller_InstallResut_Error_InvalidSources_None
                    : string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_InvalidSources, string.Join(", ", e.SourcesList));

                return InstallResult.CreateFailure(
                        installRequest,
                        InstallerErrorCode.InvalidSource,
                        message);
            }
            catch (InvalidNuGetPackageException e)
            {
                return InstallResult.CreateFailure(
                    installRequest,
                    InstallerErrorCode.InvalidPackage,
                    string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_InvalidPackage, e.PackageLocation));
            }
            catch (OperationCanceledException)
            {
                return InstallResult.CreateFailure(
                    installRequest,
                    InstallerErrorCode.GenericError,
                    LocalizableStrings.NuGetInstaller_InstallResut_Error_OperationCancelled);
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Installing {installRequest.DisplayName} failed. Details:{e}");
                return InstallResult.CreateFailure(
                    installRequest,
                    InstallerErrorCode.GenericError,
                    string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_InstallGeneric, installRequest.DisplayName, e.Message));
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
            if (templatePackage is not NuGetManagedTemplatePackage)
            {
                return Task.FromResult(UninstallResult.CreateFailure(
                    templatePackage,
                    InstallerErrorCode.UnsupportedRequest,
                    string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_PackageNotSupported, templatePackage.DisplayName, Factory.Name)));
            }
            try
            {
                _environmentSettings.Host.FileSystem.FileDelete(templatePackage.MountPointUri);
                return Task.FromResult(UninstallResult.CreateSuccess(templatePackage));
            }
            catch (Exception e)
            {
                _logger.LogDebug("Uninstalling {0} failed. Details:{1}", templatePackage.DisplayName, e);
                return Task.FromResult(UninstallResult.CreateFailure(
                    templatePackage,
                    InstallerErrorCode.GenericError,
                    string.Format(LocalizableStrings.NuGetInstaller_InstallResut_Error_UninstallGeneric, templatePackage.DisplayName, e.Message)));
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
                if (uninstallResult.ErrorMessage is null)
                {
                    throw new InvalidOperationException($"{nameof(uninstallResult.ErrorMessage)} cannot be null when {nameof(uninstallResult.Success)} is 'true'");
                }
                return UpdateResult.CreateFailure(updateRequest, uninstallResult.Error, uninstallResult.ErrorMessage);
            }

            Dictionary<string, string> installationDetails = new Dictionary<string, string>();
            if (updateRequest.TemplatePackage is NuGetManagedTemplatePackage nuGetManagedSource && !string.IsNullOrWhiteSpace(nuGetManagedSource.NuGetSource))
            {
                installationDetails.Add(InstallerConstants.NuGetSourcesKey, nuGetManagedSource.NuGetSource!);
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
                _logger.LogError(string.Format(LocalizableStrings.NuGetInstaller_Error_FailedToReadPackage, installRequest.PackageIdentifier));
                _logger.LogDebug($"Details: {ex}.");
                throw new InvalidNuGetPackageException(installRequest.PackageIdentifier, ex);
            }
            string targetPackageLocation = Path.Combine(_installPath, packageInfo.PackageIdentifier + "." + packageInfo.PackageVersion + ".nupkg");
            if (!installRequest.Force && _environmentSettings.Host.FileSystem.FileExists(targetPackageLocation))
            {
                _logger.LogError(string.Format(LocalizableStrings.NuGetInstaller_Error_CopyFailed, installRequest.PackageIdentifier, targetPackageLocation));
                _logger.LogError(string.Format(LocalizableStrings.NuGetInstaller_Error_FileAlreadyExists, targetPackageLocation));
                throw new DownloadException(packageInfo.PackageIdentifier, packageInfo.PackageVersion, installRequest.PackageIdentifier);
            }
            try
            {
                _environmentSettings.Host.FileSystem.FileCopy(installRequest.PackageIdentifier, targetPackageLocation, overwrite: installRequest.Force);
                packageInfo = packageInfo.WithFullPath(targetPackageLocation);
            }
            catch (Exception ex)
            {
                _logger.LogError(string.Format(LocalizableStrings.NuGetInstaller_Error_CopyFailed, installRequest.PackageIdentifier, targetPackageLocation), null, 0);
                _logger.LogDebug($"Details: {ex}.");
                throw new DownloadException(packageInfo.PackageIdentifier, packageInfo.PackageVersion, installRequest.PackageIdentifier);
            }
            return packageInfo;
        }

        private NuGetPackageInfo ReadPackageInformation(string packageLocation)
        {
            using Stream inputStream = _environmentSettings.Host.FileSystem.OpenRead(packageLocation);
            using PackageArchiveReader reader = new PackageArchiveReader(inputStream);

            NuspecReader nuspec = reader.NuspecReader;

            return new NuGetPackageInfo(
                nuspec.GetAuthors(),
                packageLocation,
                null,
                nuspec.GetId(),
                nuspec.GetVersion().ToNormalizedString());
        }
    }
}

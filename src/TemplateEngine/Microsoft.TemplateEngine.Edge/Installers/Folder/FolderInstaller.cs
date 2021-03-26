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

namespace Microsoft.TemplateEngine.Edge.Installers.Folder
{
    internal class FolderInstaller : IInstaller, ISerializableInstaller
    {
        private readonly IEngineEnvironmentSettings _settings;

        public FolderInstaller(IEngineEnvironmentSettings settings, IInstallerFactory factory)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IInstallerFactory Factory { get; }

        public Task<bool> CanInstallAsync(InstallRequest installationRequest, CancellationToken cancellationToken)
        {
            _ = installationRequest ?? throw new ArgumentNullException(nameof(installationRequest));

            return Task.FromResult(Directory.Exists(installationRequest.Identifier));
        }

        public IManagedTemplatePackage Deserialize(IManagedTemplatePackageProvider provider, TemplatePackageData data)
        {
            _ = provider ?? throw new ArgumentNullException(nameof(provider));
            if (data.InstallerId != Factory.Id)
            {
                throw new ArgumentException($"{nameof(FolderInstaller)} can only deserialize packages with {nameof(data.InstallerId)} {Factory.Id}", nameof(data));
            }

            return new FolderManagedTemplatePackage(_settings, this, provider, data.MountPointUri);
        }

        public Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionAsync(IEnumerable<IManagedTemplatePackage> packages, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = packages ?? throw new ArgumentNullException(nameof(packages));

            return Task.FromResult<IReadOnlyList<CheckUpdateResult>>(packages.Select(s => CheckUpdateResult.CreateSuccess(s, null, true)).ToList());
        }

        public Task<InstallResult> InstallAsync(InstallRequest installRequest, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = installRequest ?? throw new ArgumentNullException(nameof(installRequest));
            _ = provider ?? throw new ArgumentNullException(nameof(provider));

            if (Directory.Exists(installRequest.Identifier))
            {
                return Task.FromResult(InstallResult.CreateSuccess(installRequest, new FolderManagedTemplatePackage(_settings, this, provider, installRequest.Identifier)));
            }
            else
            {
                return Task.FromResult(InstallResult.CreateFailure(installRequest, InstallerErrorCode.PackageNotFound, $"The folder {installRequest.Identifier} doesn't exist"));
            }
        }

        public TemplatePackageData Serialize(IManagedTemplatePackage templatePackage)
        {
            _ = templatePackage ?? throw new ArgumentNullException(nameof(templatePackage));
            if (!(templatePackage is FolderManagedTemplatePackage))
            {
                throw new ArgumentException($"{nameof(templatePackage)} should be of type {nameof(FolderManagedTemplatePackage)}", nameof(templatePackage));
            }

            FolderManagedTemplatePackage folderTemplatePackage = templatePackage as FolderManagedTemplatePackage
                ?? throw new ArgumentException($"{nameof(templatePackage)} should be of type {nameof(FolderManagedTemplatePackage)}", nameof(templatePackage));

            return new TemplatePackageData
            {
                MountPointUri = folderTemplatePackage.MountPointUri,
                LastChangeTime = folderTemplatePackage.LastChangeTime,
                InstallerId = Factory.Id
            };
        }

        public Task<UninstallResult> UninstallAsync(IManagedTemplatePackage templatePackage, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = templatePackage ?? throw new ArgumentNullException(nameof(templatePackage));

            return Task.FromResult(UninstallResult.CreateSuccess(templatePackage));
        }

        public Task<UpdateResult> UpdateAsync(UpdateRequest updateRequest, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken)
        {
            _ = updateRequest ?? throw new ArgumentNullException(nameof(updateRequest));

            return Task.FromResult(UpdateResult.CreateSuccess(updateRequest, updateRequest.TemplatePackage));
        }
    }
}

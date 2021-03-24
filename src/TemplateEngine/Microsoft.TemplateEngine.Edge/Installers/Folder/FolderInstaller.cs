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

namespace Microsoft.TemplateEngine.Edge.Installers.Folder
{
    internal class FolderInstaller : IInstaller
    {
        private readonly IEngineEnvironmentSettings _settings;

        public FolderInstaller(IEngineEnvironmentSettings settings, IInstallerFactory factory, IManagedTemplatePackagesProvider provider)
        {
            Name = factory.Name;
            FactoryId = factory.Id;
            _settings = settings;
            Provider = provider;
        }

        public Guid FactoryId { get; }
        public string Name { get; }
        public IManagedTemplatePackagesProvider Provider { get; }

        public Task<bool> CanInstallAsync(InstallRequest installationRequest, CancellationToken cancellationToken)
        {
            _ = installationRequest ?? throw new ArgumentNullException(nameof(installationRequest));

            return Task.FromResult(Directory.Exists(installationRequest.Identifier));
        }

        public IManagedTemplatePackage Deserialize(IManagedTemplatePackagesProvider provider, TemplatePackageData data)
        {
            _ = provider ?? throw new ArgumentNullException(nameof(provider));
            _ = data ?? throw new ArgumentNullException(nameof(data));

            return new FolderManagedTemplatePackage(_settings, this, data.MountPointUri);
        }

        public Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionAsync(IEnumerable<IManagedTemplatePackage> sources, CancellationToken cancellationToken)
        {
            _ = sources ?? throw new ArgumentNullException(nameof(sources));

            return Task.FromResult<IReadOnlyList<CheckUpdateResult>>(sources.Select(s => CheckUpdateResult.CreateSuccess(s, null, true)).ToList());
        }

        public Task<InstallResult> InstallAsync(InstallRequest installRequest, CancellationToken cancellationToken)
        {
            _ = installRequest ?? throw new ArgumentNullException(nameof(installRequest));

            if (Directory.Exists(installRequest.Identifier))
            {
                return Task.FromResult(InstallResult.CreateSuccess(installRequest, new FolderManagedTemplatePackage(_settings, this, installRequest.Identifier)));
            }
            else
            {
                return Task.FromResult(InstallResult.CreateFailure(installRequest, InstallerErrorCode.PackageNotFound, $"The folder {installRequest.Identifier} doesn't exist"));
            }
        }

        public TemplatePackageData Serialize(IManagedTemplatePackage managedSource)
        {
            _ = managedSource ?? throw new ArgumentNullException(nameof(managedSource));

            return new TemplatePackageData
            {
                MountPointUri = managedSource.MountPointUri,
                LastChangeTime = managedSource.LastChangeTime,
                InstallerId = FactoryId
            };
        }

        public Task<UninstallResult> UninstallAsync(IManagedTemplatePackage managedSource, CancellationToken cancellationToken)
        {
            _ = managedSource ?? throw new ArgumentNullException(nameof(managedSource));

            return Task.FromResult(UninstallResult.CreateSuccess(managedSource));
        }

        public Task<UpdateResult> UpdateAsync(UpdateRequest updateRequest, CancellationToken cancellationToken)
        {
            _ = updateRequest ?? throw new ArgumentNullException(nameof(updateRequest));

            return Task.FromResult(UpdateResult.CreateSuccess(updateRequest, updateRequest.Source));
        }
    }
}

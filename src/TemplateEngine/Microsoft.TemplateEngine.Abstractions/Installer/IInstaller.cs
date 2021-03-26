// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    public interface IInstaller
    {
        /// <summary>
        /// Gets The ID of <see cref="IInstallerFactory."/> created the installer.
        /// </summary>
        Guid FactoryId { get; }

        /// <summary>
        /// Gets the installer name.
        /// </summary>
        /// <remarks>
        /// The caller can specify <see cref="Name"/> in <see cref="InstallRequest.InstallerName"/> to use the installer for installation.
        /// This is useful when the installer cannot be determined by using <see cref="CanInstallAsync"/>.
        /// </remarks>
        string Name { get; }

        /// <summary>
        /// Gets <see cref="IManagedTemplatePackageProvider"/> that created the installer.
        /// </summary>
        IManagedTemplatePackageProvider Provider { get; }

        /// <summary>
        /// Determines if the installer can install specific <see cref="InstallRequest"/>.
        /// </summary>
        /// <remarks>
        /// Ideally it should go as far as calling backend server to determine if such identifier exists.
        /// </remarks>
        Task<bool> CanInstallAsync(InstallRequest installationRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Gets latest versions for <paramref name="templatePackages"/>.
        /// </summary>
        /// <param name="templatePackages">the template packages to get latest versions for.</param>
        /// <returns>list of <see cref="CheckUpdateResult"/> containing latest versions for the sources.</returns>
        /// <param name="cancellationToken"></param>
        Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionAsync(IEnumerable<IManagedTemplatePackage> templatePackages, CancellationToken cancellationToken);

        /// <summary>
        /// Installs the template package defined by <paramref name="installRequest"/>.
        /// </summary>
        /// <param name="installRequest">the template package to be installed.</param>
        /// <returns><see cref="InstallResult"/> containing installation results and <see cref="IManagedTemplatePackage"/> if installation was successful.</returns>
        /// <param name="cancellationToken"></param>
        Task<InstallResult> InstallAsync(InstallRequest installRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Uninstalls the <paramref name="templatePackage"/>.
        /// </summary>
        /// <param name="templatePackage">the template package to uninstall.</param>
        /// <returns><see cref="UninstallResult"/> containing the result for operation.</returns>
        /// <param name="cancellationToken"></param>
        Task<UninstallResult> UninstallAsync(IManagedTemplatePackage templatePackage, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the template package defined by <paramref name="updateRequest"/>.
        /// </summary>
        /// <param name="updateRequest"><see cref="UpdateRequest"/> defining the template package to update and target version.</param>
        /// <returns><see cref="UpdateResult"/> containing update results and <see cref="IManagedTemplatePackage"/> if update was successful.</returns>
        /// <param name="cancellationToken"></param>
        Task<UpdateResult> UpdateAsync(UpdateRequest updateRequest, CancellationToken cancellationToken);
    }
}

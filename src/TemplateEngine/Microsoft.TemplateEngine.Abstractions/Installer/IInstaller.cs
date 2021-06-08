// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Template package installer interface.
    /// To implement installer compatible with built-in template package providers, implement this interface.
    /// </summary>
    public interface IInstaller
    {
        /// <summary>
        /// Gets the <see cref="IInstallerFactory"/> that created this installer.
        /// </summary>
        IInstallerFactory Factory { get; }

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
        /// <param name="provider"><see cref="IManagedTemplatePackageProvider"/> requesting latest version.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>list of <see cref="CheckUpdateResult"/> containing latest versions for the sources.</returns>
        Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionAsync(IEnumerable<IManagedTemplatePackage> templatePackages, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken);

        /// <summary>
        /// Installs the template package defined by <paramref name="installRequest"/>.
        /// </summary>
        /// <param name="installRequest">the template package to be installed.</param>
        /// <param name="provider"><see cref="IManagedTemplatePackageProvider"/> installing the package.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="InstallResult"/> containing installation results and installed <see cref="InstallerOperationResult.TemplatePackage"/> if installation was successful.</returns>
        Task<InstallResult> InstallAsync(InstallRequest installRequest, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken);

        /// <summary>
        /// Uninstalls the <paramref name="templatePackage"/>.
        /// </summary>
        /// <param name="templatePackage">the template package to uninstall.</param>
        /// <param name="provider"><see cref="IManagedTemplatePackageProvider"/> uninstalling the template package.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="UninstallResult"/> containing the result for operation.</returns>
        Task<UninstallResult> UninstallAsync(IManagedTemplatePackage templatePackage, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the template package defined by <paramref name="updateRequest"/>.
        /// </summary>
        /// <param name="updateRequest"><see cref="UpdateRequest"/> defining the template package to update and target version.</param>
        /// <param name="provider"><see cref="IManagedTemplatePackageProvider"/> updating the package.</param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="UpdateResult"/> containing the result for operation and installed <see cref="InstallerOperationResult.TemplatePackage"/> if update was successful.</returns>
        Task<UpdateResult> UpdateAsync(UpdateRequest updateRequest, IManagedTemplatePackageProvider provider, CancellationToken cancellationToken);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.Installer;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// The provider is responsible for managing <see cref="IManagedTemplatePackage"/>s.
    /// Besides base functionality of <see cref="ITemplatePackageProvider"/>, it adds ability to install, update and uninstall template packages.
    /// </summary>
    /// <remarks>
    /// The <see cref="IManagedTemplatePackageProvider"/> keeps track of template packages managed by the provider. The actual installation is done by <see cref="IInstaller"/> implementations.
    /// </remarks>
    public interface IManagedTemplatePackageProvider : ITemplatePackageProvider
    {
        /// <summary>
        /// Gets the latest version for the template packages.
        /// </summary>
        /// <param name="templatePackages">List of <see cref="IManagedTemplatePackage"/> to get latest version for.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>List of <see cref="CheckUpdateResult"/> containing the check results.</returns>
        Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionsAsync(IEnumerable<IManagedTemplatePackage> templatePackages, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the template packages given in <paramref name="updateRequests"/> to specified version.
        /// </summary>
        /// <param name="updateRequests">List of <see cref="UpdateRequest"/> to be processed.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>List of <see cref="UpdateResult"/> with update results.</returns>
        Task<IReadOnlyList<UpdateResult>> UpdateAsync(IEnumerable<UpdateRequest> updateRequests, CancellationToken cancellationToken);

        /// <summary>
        /// Uninstalls the template packages.
        /// </summary>
        /// <param name="templatePackages">list of <see cref="IManagedTemplatePackage"/>s to be uninstalled.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>List of <see cref="UninstallResult"/> with uninstall results.</returns>
        Task<IReadOnlyList<UninstallResult>> UninstallAsync(IEnumerable<IManagedTemplatePackage> templatePackages, CancellationToken cancellationToken);

        /// <summary>
        /// Installs new <see cref="IManagedTemplatePackage"/> based on <see cref="InstallRequest"/> data.
        /// All <see cref="IInstaller"/>s are considered via <see cref="IInstaller.CanInstallAsync(InstallRequest, CancellationToken)"/> and if only 1 <see cref="IInstaller"/>
        /// returns <c>true</c>. <see cref="IInstaller.InstallAsync(InstallRequest, CancellationToken)"/> is executed and result is returned.
        /// </summary>
        /// <param name="installRequests">Contains the list of <see cref="InstallRequest"/> to perform.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>List of <see cref="InstallResult"/> with installation results.</returns>
        Task<IReadOnlyList<InstallResult>> InstallAsync(IEnumerable<InstallRequest> installRequests, CancellationToken cancellationToken);
    }
}

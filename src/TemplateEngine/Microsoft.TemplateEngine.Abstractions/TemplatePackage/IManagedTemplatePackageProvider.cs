using Microsoft.TemplateEngine.Abstractions.Installer;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.TemplatePackage
{
    /// <summary>
    /// This provider is responsible for managing <see cref="IManagedTemplatePackage"/>.
    /// </summary>
    public interface IManagedTemplatePackageProvider : ITemplatePackageProvider
    {
        /// <summary>
        /// Takes list of <see cref="IManagedTemplatePackage"/> as input so it can check for latest versions in batch.
        /// And returns list of <see cref="CheckUpdateResult"/> which contains original <see cref="IManagedTemplatePackage"/>
        /// so caller can compare <see cref="CheckUpdateResult.LatestVersion"/> with <see cref="IManagedTemplatePackage.Version"/>
        /// </summary>
        /// <param name="managedSources">List of <see cref="IManagedTemplatePackage"/> to get latest version for.</param>
        /// <returns>List of <see cref="ManagedTemplatePackageUpdate"/></returns>
        /// <param name="cancellationToken"></param>
        Task<IReadOnlyList<CheckUpdateResult>> GetLatestVersionsAsync(IEnumerable<IManagedTemplatePackage> managedSources, CancellationToken cancellationToken);

        /// <summary>
        /// Updates specified <see cref="IManagedTemplatePackage"/>s and returns <see cref="UpdateResult"/>s which contain
        /// new <see cref="IManagedTemplatePackage"/>, if update failed <see cref="UpdateResult.Success"/> will be <c>false</c>.
        /// </summary>
        /// <param name="updateRequests">List of <see cref="IManagedTemplatePackage"/> to be updated.</param>
        /// <returns>List of <see cref="UpdateResult"/> with install information.</returns>
        /// <param name="cancellationToken"></param>
        Task<IReadOnlyList<UpdateResult>> UpdateAsync(IEnumerable<UpdateRequest> updateRequests, CancellationToken cancellationToken);

        /// <summary>
        /// Uninstalls specified <see cref="IManagedTemplatePackage"/>.
        /// </summary>
        /// <param name="managedSources">list of <see cref="IManagedTemplatePackage"/>s to be uninstalled.</param>
        /// <returns><see cref="UninstallResult"/> which has <see cref="UninstallResult.Success"/> which should be checked.</returns>
        /// <param name="cancellationToken"></param>
        Task<IReadOnlyList<UninstallResult>> UninstallAsync(IEnumerable<IManagedTemplatePackage> managedSources, CancellationToken cancellationToken);

        /// <summary>
        /// Installs new <see cref="IManagedTemplatePackage"/> based on <see cref="InstallRequest"/> data.
        /// All <see cref="IInstaller"/>s are considered via <see cref="IInstaller.CanInstallAsync(InstallRequest, CancellationToken)"/> and if only 1 <see cref="IInstaller"/>
        /// returns <c>true</c>. <see cref="IInstaller.InstallAsync(InstallRequest, CancellationToken)"/> is executed and result is returned.
        /// </summary>
        /// <param name="installRequests">Contains the list of install requests to perform.</param>
        /// <returns><see cref="InstallResult"/> containing <see cref="IManagedTemplatePackage"/>, if <see cref="InstallResult.Success" /> is <c>true</c>.</returns>
        /// <param name="cancellationToken"></param>
        Task<IReadOnlyList<InstallResult>> InstallAsync(IEnumerable<InstallRequest> installRequests, CancellationToken cancellationToken);
    }
}

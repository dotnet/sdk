// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Represents the result of template package update using <see cref="IInstaller.UpdateAsync"/>.
    /// </summary>
    public sealed class UpdateResult : InstallerOperationResult
    {
        private UpdateResult(UpdateRequest request, IManagedTemplatePackage templatePackage, IReadOnlyList<VulnerabilityInfo> vulnerabilities)
            : base(templatePackage)
        {
            UpdateRequest = request;
            Vulnerabilities = vulnerabilities;
        }

        private UpdateResult(UpdateRequest request, InstallerErrorCode error, string errorMessage, IReadOnlyList<VulnerabilityInfo> vulnerabilities)
             : base(error, errorMessage)
        {
            UpdateRequest = request;
            Vulnerabilities = vulnerabilities;
        }

        private UpdateResult(UpdateRequest request, InstallResult installResult)
            : base(installResult.Error, installResult.ErrorMessage, installResult.TemplatePackage)
        {
            UpdateRequest = request;
            Vulnerabilities = installResult.Vulnerabilities;
        }

        /// <summary>
        /// <see cref="UpdateRequest"/> processed by <see cref="IInstaller.UpdateAsync"/> operation.
        /// </summary>
        public UpdateRequest UpdateRequest { get; private set; }

        /// <summary>
        /// Gets vulnerabilities from installed package.
        /// </summary>
        public IReadOnlyList<VulnerabilityInfo> Vulnerabilities { get; private set; }

        /// <summary>
        /// Creates successful result for the operation.
        /// </summary>
        /// <param name="request">the processed <see cref="UpdateRequest"/>.</param>
        /// <param name="templatePackage">the updated <see cref="IManagedTemplatePackage"/>.</param>
        /// <param name="vulnerabilities">Package vulnerabilities associated with the update request.</param>
        /// <returns></returns>
        public static UpdateResult CreateSuccess(UpdateRequest request, IManagedTemplatePackage templatePackage, IReadOnlyList<VulnerabilityInfo> vulnerabilities)
        {
            return new UpdateResult(request, templatePackage, vulnerabilities);
        }

        /// <summary>
        /// Creates failure result for the operation.
        /// </summary>
        /// <param name="request">the processed <see cref="UpdateRequest"/>.</param>
        /// <param name="error">error code, see <see cref="InstallerErrorCode"/> for details.</param>
        /// <param name="localizedFailureMessage">detailed error message.</param>
        /// <param name="vulnerabilities">Package vulnerabilities associated with the update request.</param>
        /// <returns></returns>
        public static UpdateResult CreateFailure(UpdateRequest request, InstallerErrorCode error, string localizedFailureMessage, IReadOnlyList<VulnerabilityInfo> vulnerabilities)
        {
            return new UpdateResult(request, error, localizedFailureMessage, vulnerabilities);
        }

        /// <summary>
        /// Creates <see cref="UpdateResult"/> from <see cref="InstallResult"/>.
        /// </summary>
        /// <param name="request">the processed <see cref="UpdateRequest"/>.</param>
        /// <param name="installResult"><see cref="InstallResult"/> to be converted to <see cref="UpdateResult"/>.</param>
        /// <returns></returns>
        public static UpdateResult FromInstallResult(UpdateRequest request, InstallResult installResult)
        {
            return new UpdateResult(request, installResult);
        }
    }
}

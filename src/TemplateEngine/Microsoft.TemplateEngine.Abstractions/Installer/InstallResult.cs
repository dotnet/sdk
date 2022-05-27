// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Represents the result of template package installation using <see cref="IInstaller.InstallAsync"/>.
    /// </summary>
    public sealed class InstallResult : InstallerOperationResult
    {
        private InstallResult(InstallRequest request, IManagedTemplatePackage templatePackage)
            : base(templatePackage)
        {
            InstallRequest = request;
        }

        private InstallResult(InstallRequest request, InstallerErrorCode error, string errorMessage)
             : base(error, errorMessage)
        {
            InstallRequest = request;
        }

        /// <summary>
        /// <see cref="InstallRequest"/> processed by <see cref="IInstaller.InstallAsync"/> operation.
        /// </summary>
        public InstallRequest InstallRequest { get; private set; }

        /// <summary>
        /// Creates successful result for the operation.
        /// </summary>
        /// <param name="request">the processed installation request.</param>
        /// <param name="templatePackage">the installed <see cref="IManagedTemplatePackage"/>.</param>
        /// <returns></returns>
        public static InstallResult CreateSuccess(InstallRequest request, IManagedTemplatePackage templatePackage)
        {
            return new InstallResult(request, templatePackage);
        }

        /// <summary>
        /// Creates failure result for the operation.
        /// </summary>
        /// <param name="request">the processed installation request.</param>
        /// <param name="error">error code, see <see cref="InstallerErrorCode"/> for details.</param>
        /// <param name="localizedFailureMessage">detailed error message.</param>
        /// <returns></returns>
        public static InstallResult CreateFailure(InstallRequest request, InstallerErrorCode error, string localizedFailureMessage)
        {
            return new InstallResult(request, error, localizedFailureMessage);
        }
    }
}

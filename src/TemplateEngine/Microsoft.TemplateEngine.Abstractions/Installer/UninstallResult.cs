// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Represents the result of template package installation using <see cref="IInstaller.InstallAsync"/>.
    /// </summary>
    public sealed class UninstallResult : Result
    {
        private UninstallResult() { }

        /// <summary>
        /// Creates successful result for the operation.
        /// </summary>
        /// <param name="templatePackage">the uninstalled <see cref="IManagedTemplatePackage"/>.</param>
        /// <returns></returns>
        public static UninstallResult CreateSuccess(IManagedTemplatePackage templatePackage)
        {
            return new UninstallResult()
            {
                Error = InstallerErrorCode.Success,
                TemplatePackage = templatePackage
            };
        }

        /// <summary>
        /// Creates failure result for the operation.
        /// </summary>
        /// <param name="templatePackage">the template package attempted to be uninstalled.</param>
        /// <param name="error">error code, see <see cref="InstallerErrorCode"/> for details.</param>
        /// <param name="localizedFailureMessage">detailed error message.</param>
        /// <returns></returns>
        public static UninstallResult CreateFailure(IManagedTemplatePackage templatePackage, InstallerErrorCode error, string localizedFailureMessage)
        {
            return new UninstallResult()
            {
                TemplatePackage = templatePackage,
                Error = error,
                ErrorMessage = localizedFailureMessage
            };
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Represents the result of checking the latest version of template package using <see cref="IInstaller.GetLatestVersionAsync"/>.
    /// </summary>
    public sealed class CheckUpdateResult : InstallerOperationResult
    {
        private CheckUpdateResult() { }

        /// <summary>
        /// The latest version for the template package identified as the result of update check.
        /// </summary>
        public string LatestVersion { get; private set; }

        /// <summary>
        /// True when the template package is already at the latest version.
        /// </summary>
        /// <remarks>In some cases the version installed can be higher then identified during update check. Since only installer can correctly compare the versions, the installer returns the result if the version is latest rather than letting the caller determine it using string comparison.</remarks>
        public bool IsLatestVersion { get; private set; }

        /// <summary>
        /// Creates successful result for the operation.
        /// </summary>
        /// <param name="templatePackage">the package that was checked for update.</param>
        /// <param name="version">the latest version of the package.</param>
        /// <param name="isLatest">indication if installed version is latest or higher.</param>
        /// <returns></returns>
        public static CheckUpdateResult CreateSuccess(IManagedTemplatePackage templatePackage, string version, bool isLatest)
        {
            return new CheckUpdateResult()
            {
                Error = InstallerErrorCode.Success,
                TemplatePackage = templatePackage,
                LatestVersion = version,
                IsLatestVersion = isLatest,
            };
        }

        /// <summary>
        /// Creates failure result for the operation.
        /// </summary>
        /// <param name="templatePackage">the package that was checked for update.</param>
        /// <param name="error">error code, see <see cref="InstallerErrorCode"/> for details.</param>
        /// <param name="localizedFailureMessage">detailed error message.</param>
        /// <returns></returns>
        public static CheckUpdateResult CreateFailure(IManagedTemplatePackage templatePackage, InstallerErrorCode error, string localizedFailureMessage)
        {
            return new CheckUpdateResult()
            {
                Error = error,
                ErrorMessage = localizedFailureMessage,
                TemplatePackage = templatePackage
            };
        }
    }
}

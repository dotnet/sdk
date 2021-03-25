// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Represents the result of template package update using <see cref="IInstaller.UpdateAsync"/>.
    /// </summary>
    public sealed class UpdateResult : Result
    {
        private UpdateResult() { }

        /// <summary>
        /// <see cref="UpdateRequest"/> processed by <see cref="IInstaller.UpdateAsync"/> operation.
        /// </summary>
        public UpdateRequest UpdateRequest { get; private set; }

        /// <summary>
        /// Creates successful result for the operation.
        /// </summary>
        /// <param name="request">the processed <see cref="UpdateRequest"/>.</param>
        /// <param name="templatePackage">the updated <see cref="IManagedTemplatePackage"/>.</param>
        /// <returns></returns>
        public static UpdateResult CreateSuccess(UpdateRequest request, IManagedTemplatePackage templatePackage)
        {
            return new UpdateResult()
            {
                UpdateRequest = request,
                Error = InstallerErrorCode.Success,
                TemplatePackage = templatePackage
            };
        }

        /// <summary>
        /// Creates failure result for the operation.
        /// </summary>
        /// <param name="request">the processed <see cref="UpdateRequest"/>.</param>
        /// <param name="error">error code, see <see cref="InstallerErrorCode"/> for details.</param>
        /// <param name="localizedFailureMessage">detailed error message.</param>
        /// <returns></returns>
        public static UpdateResult CreateFailure(UpdateRequest request, InstallerErrorCode error, string localizedFailureMessage)
        {
            return new UpdateResult()
            {
                UpdateRequest = request,
                Error = error,
                ErrorMessage = localizedFailureMessage
            };
        }

        /// <summary>
        /// Creates <see cref="UpdateResult"/> from <see cref="InstallResult"/>.
        /// </summary>
        /// <param name="request">the processed <see cref="UpdateRequest"/>.</param>
        /// <param name="installResult"><see cref="InstallResult"/> to be converted to <see cref="UpdateResult"/>.</param>
        /// <returns></returns>
        public static UpdateResult FromInstallResult(UpdateRequest request, InstallResult installResult)
        {
            return new UpdateResult()
            {
                UpdateRequest = request,
                TemplatePackage = installResult.TemplatePackage,
                Error = installResult.Error,
                ErrorMessage = installResult.ErrorMessage
            };
        }
    }
}

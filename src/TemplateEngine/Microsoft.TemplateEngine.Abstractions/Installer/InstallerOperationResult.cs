// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Represents <see cref="IInstaller"/> operation result.
    /// </summary>
    public abstract class InstallerOperationResult
    {
        protected InstallerOperationResult(InstallerErrorCode error, string? errorMessage, IManagedTemplatePackage? templatePackage = null)
        {
            Error = error;
            ErrorMessage = errorMessage;
            TemplatePackage = templatePackage;
        }

        protected InstallerOperationResult(IManagedTemplatePackage managedTemplatePackage)
        {
            Error = InstallerErrorCode.Success;
            TemplatePackage = managedTemplatePackage;
        }

        /// <summary>
        /// Gets result error code.
        /// <see cref="InstallerErrorCode.Success"/> if the operation completed successfully.
        /// </summary>
        public InstallerErrorCode Error { get; }

        /// <summary>
        /// Error message for failed operation. Not set if the operation completed successfully.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// <see cref="IManagedTemplatePackage"/> processed by the operation.
        /// </summary>
        public virtual IManagedTemplatePackage? TemplatePackage { get; }

        /// <summary>
        /// Indicates if the operation completed successfully.
        /// </summary>
        public bool Success => Error == InstallerErrorCode.Success;
    }
}

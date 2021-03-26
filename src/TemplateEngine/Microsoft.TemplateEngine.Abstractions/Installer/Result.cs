// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Represents <see cref="IInstaller"/> operation result.
    /// </summary>
    public abstract class Result
    {
        /// <summary>
        /// Error code, <seealso cref="InstallerErrorCode"/>.
        /// <see cref="InstallerErrorCode.Success"/> if the operation completed successfully.
        /// </summary>
        public InstallerErrorCode Error { get; protected set; }

        /// <summary>
        /// Error message for failed operation. Not set if the operation completed successfully.
        /// </summary>
        public string ErrorMessage { get; protected set; }

        /// <summary>
        /// <see cref="IManagedTemplatePackage"/> processed by the operation.
        /// </summary>
        public IManagedTemplatePackage TemplatePackage { get; protected set; }

        /// <summary>
        /// Indicates if the operation completed successfully.
        /// </summary>
        public bool Success => Error == InstallerErrorCode.Success;
    }
}

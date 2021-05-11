// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    /// <summary>
    /// Defines possible error codes for <see cref="IInstaller"/> operations.
    /// </summary>
    public enum InstallerErrorCode
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The template package is not found.
        /// </summary>
        PackageNotFound = 1,

        /// <summary>
        /// The installation source (e.g. NuGet feed) is invalid.
        /// </summary>
        InvalidSource = 2,

        /// <summary>
        /// The download from remote source (e.g. NuGet feed) has failed.
        /// </summary>
        DownloadFailed = 3,

        /// <summary>
        /// The  request is not supported by the installer.
        /// </summary>
        UnsupportedRequest = 4,

        /// <summary>
        /// Generic error.
        /// </summary>
        GenericError = 5,

        /// <summary>
        /// The template package is already installed.
        /// </summary>
        AlreadyInstalled = 6,

        /// <summary>
        /// The update has failed due to uninstallation of previous template package version has failed.
        /// </summary>
        UpdateUninstallFailed = 7,

        /// <summary>
        /// The requested package is invalid and cannot be processed.
        /// </summary>
        InvalidPackage = 8
    }
}

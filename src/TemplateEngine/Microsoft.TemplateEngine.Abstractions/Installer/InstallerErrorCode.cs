// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions.Installer
{
    public enum InstallerErrorCode
    {
        Success = 0,
        PackageNotFound = 1,
        InvalidSource = 2,
        DownloadFailed = 3,
        UnsupportedRequest = 4,
        GenericError = 5,
        AlreadyInstalled = 6,
        UpdateUninstallFailed = 7,
        InvalidPackage = 8
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal interface IInstallationValidator
{
    bool Validate(DotnetInstall install);

    /// <summary>
    /// Validates a .NET installation and describes why validation failed.
    /// </summary>
    bool Validate(DotnetInstall install, out string? failureReason)
    {
        bool isValid = Validate(install);
        failureReason = isValid ? null : $"{install.Component} {install.Version} was not found at '{install.InstallRoot.Path}'.";
        return isValid;
    }
}

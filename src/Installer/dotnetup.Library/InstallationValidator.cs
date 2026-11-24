// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Validates an installation from its on-disk layout, for both archive and admin installer installs. The
/// caller supplies the install root (the user root, or the system root in admin mode).
/// </summary>
internal sealed class InstallationValidator : IInstallationValidator
{
#pragma warning disable CA1822 // Validate methods implement IInstallationValidator and cannot be made static
    public bool Validate(DotnetInstall install)
    {
        return Validate(install, out _);
    }

    /// <summary>
    /// Validates a .NET installation with detailed failure reasons.
    /// </summary>
    /// <param name="install">The install to validate.</param>
    /// <param name="failureReason">If validation fails, describes what went wrong.</param>
    /// <returns>True if the installation is valid.</returns>
    public bool Validate(DotnetInstall install, out string? failureReason)
    {
        failureReason = null;
        string? installRoot = install.InstallRoot.Path;
        if (string.IsNullOrEmpty(installRoot))
        {
            failureReason = "Install root path is null or empty.";
            return false;
        }

        string dotnetMuxerPath = GetMuxerPath(installRoot);
        if (!File.Exists(dotnetMuxerPath))
        {
            if (install.Component == InstallComponent.WindowsDesktop && PassesValidationSmokeCheck(install))
            {
                return true;
            }
            failureReason = $"Muxer not found at '{dotnetMuxerPath}'.";
            return false;
        }

        if (!PassesValidationSmokeCheck(install))
        {
            failureReason = $"Component layout validation failed. Expected '{install.ComponentDirectory}' to contain {(install.Component == InstallComponent.SDK ? "dotnet.dll" : "the framework .deps.json")}.";
            return false;
        }

        // We should also validate whether the host is the maximum version or higher than all installed versions.

        return true;
    }
#pragma warning restore CA1822

    /// <summary>
    /// A spot check, not a full validity check: matches how hostfxr recognizes installs. An SDK version folder
    /// must contain <c>dotnet.dll</c>, and a runtime version folder must contain <c>&lt;framework name&gt;.deps.json</c>.
    /// The folder and marker are derived from the install's component and on-disk version.
    /// </summary>
    internal static bool PassesValidationSmokeCheck(DotnetInstall install)
    {
        string markerFile = install.Component == InstallComponent.SDK
            ? "dotnet.dll"
            : install.Component.GetFrameworkName() + ".deps.json";
        return File.Exists(Path.Combine(install.ComponentDirectory, markerFile));
    }

    /// <summary>Gets the path of the <c>dotnet</c> muxer in an install root.</summary>
    internal static string GetMuxerPath(string installRoot) => Path.Combine(installRoot, DotnetupUtilities.GetDotnetExeName());
}

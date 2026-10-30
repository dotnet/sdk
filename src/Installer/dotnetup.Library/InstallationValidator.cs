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
    private static readonly Dictionary<InstallComponent, string> s_runtimeMonikerByComponent = new()
    {
        [InstallComponent.Runtime] = InstallComponentExtensions.RuntimeFrameworkName,
        [InstallComponent.ASPNETCore] = InstallComponentExtensions.AspNetCoreFrameworkName,
        [InstallComponent.WindowsDesktop] = InstallComponentExtensions.WindowsDesktopFrameworkName
    };

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
            if (install.Component == InstallComponent.WindowsDesktop)
            {
                string resolvedVersionLayout = install.Version.ToString();
                if (ValidateComponentLayout(installRoot, resolvedVersionLayout, install.Component))
                {
                    return true;
                }
            }
            failureReason = $"Muxer not found at '{dotnetMuxerPath}'.";
            return false;
        }

        string resolvedVersion = install.Version.ToString();
        if (!ValidateComponentLayout(installRoot, resolvedVersion, install.Component))
        {
            string expectedDir = GetComponentDirectory(installRoot, install.Component, resolvedVersion) ?? "<unknown>";
            failureReason = $"Component layout validation failed. Expected '{expectedDir}' to contain {(install.Component == InstallComponent.SDK ? "dotnet.dll" : "the framework .deps.json")}.";
            return false;
        }

        // We should also validate whether the host is the maximum version or higher than all installed versions.

        return true;
    }
#pragma warning restore CA1822

    /// <summary>
    /// Matches how hostfxr recognizes installs: an SDK version folder must contain <c>dotnet.dll</c>, and a
    /// runtime version folder must contain <c>&lt;framework name&gt;.deps.json</c>.
    /// </summary>
    private static bool ValidateComponentLayout(string installRoot, string resolvedVersion, InstallComponent component)
    {
        string? componentDirectory = GetComponentDirectory(installRoot, component, resolvedVersion);
        if (componentDirectory is null)
        {
            return false;
        }

        string markerFile = component == InstallComponent.SDK
            ? "dotnet.dll"
            : s_runtimeMonikerByComponent[component] + ".deps.json";
        return File.Exists(Path.Combine(componentDirectory, markerFile));
    }

    /// <summary>Gets the path of the <c>dotnet</c> muxer in an install root.</summary>
    internal static string GetMuxerPath(string installRoot) => Path.Combine(installRoot, DotnetupUtilities.GetDotnetExeName());

    /// <summary>Gets the directory a specific version of a component is installed to, or null for an unknown component.</summary>
    internal static string? GetComponentDirectory(string installRoot, InstallComponent component, string resolvedVersion)
    {
        if (component == InstallComponent.SDK)
        {
            return Path.Combine(installRoot, "sdk", resolvedVersion);
        }

        return s_runtimeMonikerByComponent.TryGetValue(component, out string? runtimeMoniker)
            ? Path.Combine(installRoot, "shared", runtimeMoniker, resolvedVersion)
            : null;
    }
}

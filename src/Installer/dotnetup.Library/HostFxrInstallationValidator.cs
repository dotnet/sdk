// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Validates an installation by asking the install root's hostfxr, through that root's muxer in a child
/// process, which SDKs and runtimes it can see. Used for installations laid down by the Windows installer
/// bundles, whose layout dotnetup does not own.
/// Never throws: a missing install root, a missing hostfxr, or a query failure reports "not installed".
/// </summary>
internal sealed class HostFxrInstallationValidator : IInstallationValidator
{
    private readonly Func<string, IEnumerable<DotnetInstall>> _getInstalls;

    public HostFxrInstallationValidator()
        : this(MuxerInstallQuery.GetInstalls)
    {
    }

    internal HostFxrInstallationValidator(Func<string, IEnumerable<DotnetInstall>> getInstalls)
    {
        _getInstalls = getInstalls;
    }

    public bool Validate(DotnetInstall install) => Validate(install, out _);

    public bool Validate(DotnetInstall install, out string? failureReason)
    {
        string? installRoot = install.InstallRoot.Path;
        if (string.IsNullOrEmpty(installRoot) || !Directory.Exists(installRoot))
        {
            failureReason = $"Install root '{installRoot}' does not exist.";
            return false;
        }

        if (!HasHostFxr(installRoot))
        {
            failureReason = $"No hostfxr library was found under '{Path.Combine(installRoot, "host", "fxr")}'.";
            return false;
        }

        List<DotnetInstall> installs;
        try
        {
            installs = [.. _getInstalls(installRoot)];
        }
        catch (Exception ex)
        {
            failureReason = $"hostfxr could not enumerate the installations at '{installRoot}': {ex.Message}";
            return false;
        }

        string expectedVersion = install.Version.ToString();
        bool found = installs.Any(i =>
            i.Component == install.Component &&
            string.Equals(i.Version.ToString(), expectedVersion, StringComparison.OrdinalIgnoreCase));

        failureReason = found
            ? null
            : $"hostfxr did not report {install.Component} {expectedVersion} at '{installRoot}'. Found: [{string.Join(", ", installs.Select(i => $"{i.Component} {i.Version}"))}]";
        return found;
    }

    private static bool HasHostFxr(string installRoot)
    {
        string hostFxrDirectory = Path.Combine(installRoot, "host", "fxr");
        if (!Directory.Exists(hostFxrDirectory))
        {
            return false;
        }

        string libraryName = OperatingSystem.IsWindows()
            ? "hostfxr.dll"
            : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";

        try
        {
            return Directory.EnumerateFiles(hostFxrDirectory, libraryName, SearchOption.AllDirectories).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

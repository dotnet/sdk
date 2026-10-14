// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Resolves the shared system folder that the Windows installer bundles install into for a given
/// payload architecture. Admin mode never accepts a user-supplied install path.
/// </summary>
/// <remarks>
/// | OS architecture | Payload | Install root |
/// | x64   | x64   | Program Files\dotnet |
/// | x64   | x86   | Program Files (x86)\dotnet |
/// | Arm64 | Arm64 | Program Files\dotnet |
/// | Arm64 | x64   | Program Files\dotnet\x64 |
/// </remarks>
internal sealed class AdminInstallRootResolver
{
    private readonly Architecture _osArchitecture;
    private readonly string _programFiles;
    private readonly string _programFilesX86;

    public AdminInstallRootResolver()
        : this(
            RuntimeInformation.OSArchitecture,
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86))
    {
    }

    internal AdminInstallRootResolver(Architecture osArchitecture, string programFiles, string programFilesX86)
    {
        _osArchitecture = osArchitecture;
        _programFiles = programFiles;
        _programFilesX86 = programFilesX86;
    }

    /// <summary>Returns the install root that the installer bundle for <paramref name="architecture"/> uses.</summary>
    /// <exception cref="DotnetInstallException">The OS and payload architecture combination is not supported.</exception>
    public DotnetInstallRoot Resolve(InstallArchitecture architecture) => new(ResolvePath(architecture), architecture);

    private string ResolvePath(InstallArchitecture architecture)
    {
        InstallArchitecture osArchitecture = GetOsInstallArchitecture();

        if (architecture == osArchitecture)
        {
            return Path.Combine(RequireFolder(_programFiles), "dotnet");
        }

        if (architecture == InstallArchitecture.x86)
        {
            return Path.Combine(RequireFolder(_programFilesX86), "dotnet");
        }

        if (osArchitecture == InstallArchitecture.arm64 && architecture == InstallArchitecture.x64)
        {
            return Path.Combine(RequireFolder(_programFiles), "dotnet", "x64");
        }

        throw Unsupported($"Admin mode cannot install {architecture} .NET on a {_osArchitecture} operating system.");
    }

    private InstallArchitecture GetOsInstallArchitecture()
    {
        try
        {
            return InstallerUtilities.GetInstallArchitecture(_osArchitecture);
        }
        catch (NotSupportedException)
        {
            throw Unsupported($"Admin mode does not support the {_osArchitecture} operating system architecture.");
        }
    }

    private static string RequireFolder(string folder)
    {
        return string.IsNullOrEmpty(folder)
            ? throw Unsupported("Admin mode requires the Windows Program Files folder, which could not be determined.")
            : folder;
    }

    private static DotnetInstallException Unsupported(string message) =>
        new(DotnetInstallErrorCode.PlatformNotSupported, message);
}

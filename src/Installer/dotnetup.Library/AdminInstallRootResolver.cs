// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
/// | Arm64 | x86   | Program Files (x86)\dotnet |
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
    /// <remarks>
    /// Callers currently pass dotnetup's own process architecture, which is assumed to be the OS-native
    /// architecture, so the result is <c>Program Files\dotnet</c>, the root the installers usually put on
    /// the machine PATH. Nothing checks that PATH resolves <c>dotnet</c> to the returned root: for a
    /// non-native architecture (<c>Program Files (x86)\dotnet</c> or <c>Program Files\dotnet\x64</c>), or
    /// when PATH has been customized, the <c>dotnet</c> on PATH may belong to a different architecture.
    /// Revisit this, and any code that assumes the root is the one on PATH, when <c>--arch</c> support is added.
    /// </remarks>
    /// <exception cref="DotnetInstallException">The OS and payload architecture combination is not supported.</exception>
    public DotnetInstallRoot Resolve(InstallArchitecture architecture) =>
        new(ResolvePath(architecture, out string error) ?? throw Unsupported(error), architecture);

    /// <summary>
    /// Returns the install root of every architecture this operating system supports, native architecture first.
    /// </summary>
    public IEnumerable<DotnetInstallRoot> ResolveSupported()
    {
        if (!TryGetOsInstallArchitecture(out InstallArchitecture osArchitecture))
        {
            yield break;
        }

        foreach (var architecture in Enum.GetValues<InstallArchitecture>().OrderBy(a => a != osArchitecture))
        {
            if (ResolvePath(architecture, out _) is string path)
            {
                yield return new(path, architecture);
            }
        }
    }

    private string? ResolvePath(InstallArchitecture architecture, out string error)
    {
        error = string.Empty;
        if (!TryGetOsInstallArchitecture(out InstallArchitecture osArchitecture))
        {
            error = $"Admin mode does not support the {_osArchitecture} operating system architecture.";
            return null;
        }

        string? path = null;
        if (architecture == osArchitecture)
        {
            path = CombineWithFolder(_programFiles, "dotnet");
        }
        else if (architecture == InstallArchitecture.x86)
        {
            path = CombineWithFolder(_programFilesX86, "dotnet");
        }
        else if (osArchitecture == InstallArchitecture.arm64 && architecture == InstallArchitecture.x64)
        {
            path = CombineWithFolder(_programFiles, "dotnet", "x64");
        }
        else
        {
            error = $"Admin mode cannot install {architecture} .NET on a {_osArchitecture} operating system.";
            return null;
        }

        if (path is null)
        {
            error = "Admin mode requires the Windows Program Files folder, which could not be determined.";
        }

        return path;
    }

    private bool TryGetOsInstallArchitecture(out InstallArchitecture osArchitecture)
    {
        try
        {
            osArchitecture = InstallerUtilities.GetInstallArchitecture(_osArchitecture);
            return true;
        }
        catch (NotSupportedException)
        {
            osArchitecture = default;
            return false;
        }
    }

    private static string? CombineWithFolder(string folder, params string[] relativePath) =>
        string.IsNullOrEmpty(folder) ? null : Path.Combine([folder, .. relativePath]);

    private static DotnetInstallException Unsupported(string message) =>
        new(DotnetInstallErrorCode.PlatformNotSupported, message);
}

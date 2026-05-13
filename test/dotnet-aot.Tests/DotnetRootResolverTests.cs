// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;
using Xunit;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
///  Tests for <see cref="DotnetRootResolver"/> with injectable dependencies.
///  Validates DOTNET_ROOT resolution, architecture-specific env vars,
///  process path walk-up, and hostfxr discovery.
///  Path construction uses Path.Combine for cross-platform compatibility.
/// </summary>
public class DotnetRootResolverTests
{
    // Helper to build platform-appropriate paths for test inputs/outputs.
    // When isWindows=true, uses a Windows-style root; otherwise Unix-style.
    private static string BuildPath(bool isWindows, params string[] segments)
    {
        string root = isWindows ? @"C:\" : "/";
        return segments.Length == 0 ? root : Path.Combine(root, Path.Combine(segments));
    }

    [Fact]
    public void ResolveDotnetRoot_WithDotnetRootEnvVar_ReturnsThatPath()
    {
        string dotnetDir = BuildPath(true, "dotnet");
        string fallback = BuildPath(true, "fallback") + Path.DirectorySeparatorChar;

        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: name => name == "DOTNET_ROOT" ? dotnetDir : null,
            processPath: null,
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: _ => true,
            fileExists: _ => false,
            baseDirectory: fallback);

        Assert.Equal(dotnetDir, result);
    }

    [Fact]
    public void ResolveDotnetRoot_WithArchSpecificEnvVar_OnWindows_ReturnsThatPath()
    {
        string dotnetX64 = BuildPath(true, "dotnet-x64");
        string fallback = BuildPath(true, "fallback") + Path.DirectorySeparatorChar;

        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: name => name == "DOTNET_ROOT(x64)" ? dotnetX64 : null,
            processPath: null,
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: _ => true,
            fileExists: _ => false,
            baseDirectory: fallback);

        Assert.Equal(dotnetX64, result);
    }

    [Fact]
    public void ResolveDotnetRoot_ArchSpecificEnvVar_IgnoredOnNonWindows()
    {
        string baseDir = BuildPath(false, "usr", "lib", "dotnet") + Path.DirectorySeparatorChar;
        string expected = Path.GetDirectoryName(
            baseDir.TrimEnd(Path.DirectorySeparatorChar))!;

        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: name => name == "DOTNET_ROOT(x64)" ? BuildPath(false, "usr", "share", "dotnet-x64") : null,
            processPath: null,
            processArch: Architecture.X64,
            isWindows: false,
            directoryExists: _ => true,
            fileExists: _ => false,
            baseDirectory: baseDir);

        // On non-Windows, arch-specific env vars are ignored; falls to baseDirectory parent
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveDotnetRoot_WithDotnetRootNotExisting_SkipsIt()
    {
        string nonexistent = BuildPath(true, "nonexistent");
        string baseDir = BuildPath(true, "fallback", "sdk") + Path.DirectorySeparatorChar;
        string expected = BuildPath(true, "fallback");

        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: name => name == "DOTNET_ROOT" ? nonexistent : null,
            processPath: null,
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: path => path != nonexistent,
            fileExists: _ => false,
            baseDirectory: baseDir);

        // Non-existent DOTNET_ROOT is skipped; falls to baseDirectory parent
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveDotnetRoot_WalksUpFromProcessPath()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string processPath = Path.Combine(dotnetRoot, "sdk", "11.0.100", "dn.exe");
        string dotnetExe = Path.Combine(dotnetRoot, "dotnet.exe");
        string fallback = BuildPath(true, "fallback") + Path.DirectorySeparatorChar;

        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: _ => null,
            processPath: processPath,
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: _ => false,
            fileExists: path => path == dotnetExe,
            baseDirectory: fallback);

        Assert.Equal(dotnetRoot, result);
    }

    [Fact]
    public void ResolveDotnetRoot_NoDotnetAncestor_FallsBackToBaseDirectory()
    {
        string processPath = BuildPath(true, "app", "bin", "myapp.exe");
        string baseDir = BuildPath(true, "fallback", "sdk") + Path.DirectorySeparatorChar;
        string expected = BuildPath(true, "fallback");

        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: _ => null,
            processPath: processPath,
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: _ => false,
            fileExists: _ => false,
            baseDirectory: baseDir);

        // Last resort: parent of baseDirectory
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveHostfxrPath_WithValidFxrDir_ReturnsPath()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "9.0.0");
        string expectedPath = Path.Combine(fxrVersion, "hostfxr.dll");

        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: path => path == fxrDir,
            getDirectories: _ => new[] { fxrVersion },
            fileExists: path => path == expectedPath);

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void ResolveHostfxrPath_PicksHighestVersion()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string v800 = Path.Combine(fxrDir, "8.0.0");
        string v901 = Path.Combine(fxrDir, "9.0.1");
        string v900 = Path.Combine(fxrDir, "9.0.0");
        string expectedPath = Path.Combine(v901, "hostfxr.dll");

        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => new[] { v800, v901, v900 },
            fileExists: _ => true);

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void ResolveHostfxrPath_SkipsPrereleaseDirectories()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string preview = Path.Combine(fxrDir, "10.0.0-preview.5");
        string v900 = Path.Combine(fxrDir, "9.0.0");
        string expectedPath = Path.Combine(v900, "hostfxr.dll");

        // Version.TryParse fails for prerelease strings like "10.0.0-preview.5"
        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => new[] { preview, v900 },
            fileExists: _ => true);

        // Should pick 9.0.0 since the preview dir is skipped
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void ResolveHostfxrPath_MissingFxrDirectory_ReturnsEmpty()
    {
        string dotnetRoot = BuildPath(true, "dotnet");

        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => false,
            getDirectories: _ => Array.Empty<string>(),
            fileExists: _ => false);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveHostfxrPath_FxrDirExistsButNoHostfxrFile_ReturnsEmpty()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "9.0.0");

        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => new[] { fxrVersion },
            fileExists: _ => false);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveHostfxrPath_OnMacOS_LooksForDylib()
    {
        string dotnetRoot = Path.Combine("/", "usr", "local", "share", "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "9.0.0");
        string expectedPath = Path.Combine(fxrVersion, "libhostfxr.dylib");

        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: false,
            isMacOS: true,
            directoryExists: path => path == fxrDir,
            getDirectories: _ => new[] { fxrVersion },
            fileExists: path => path == expectedPath);

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void ResolveHostfxrPath_OnLinux_LooksForSo()
    {
        string dotnetRoot = Path.Combine("/", "usr", "share", "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "9.0.0");
        string expectedPath = Path.Combine(fxrVersion, "libhostfxr.so");

        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: false,
            isMacOS: false,
            directoryExists: path => path == fxrDir,
            getDirectories: _ => new[] { fxrVersion },
            fileExists: path => path == expectedPath);

        Assert.Equal(expectedPath, result);
    }
}

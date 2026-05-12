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
/// </summary>
public class DotnetRootResolverTests
{
    [Fact]
    public void ResolveDotnetRoot_WithDotnetRootEnvVar_ReturnsThatPath()
    {
        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: name => name == "DOTNET_ROOT" ? @"C:\dotnet" : null,
            processPath: null,
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: _ => true,
            fileExists: _ => false,
            baseDirectory: @"C:\fallback\");

        Assert.Equal(@"C:\dotnet", result);
    }

    [Fact]
    public void ResolveDotnetRoot_WithArchSpecificEnvVar_OnWindows_ReturnsThatPath()
    {
        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: name => name == "DOTNET_ROOT(x64)" ? @"C:\dotnet-x64" : null,
            processPath: null,
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: _ => true,
            fileExists: _ => false,
            baseDirectory: @"C:\fallback\");

        Assert.Equal(@"C:\dotnet-x64", result);
    }

    [Fact]
    public void ResolveDotnetRoot_ArchSpecificEnvVar_IgnoredOnNonWindows()
    {
        string baseDir = "/usr/lib/dotnet/";
        // Expected: parent of baseDirectory (Path.GetDirectoryName normalizes separators on Windows)
        string expected = Path.GetDirectoryName(
            baseDir.TrimEnd(Path.DirectorySeparatorChar))!;

        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: name => name == "DOTNET_ROOT(x64)" ? "/usr/share/dotnet-x64" : null,
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
        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: name => name == "DOTNET_ROOT" ? @"C:\nonexistent" : null,
            processPath: null,
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: path => path != @"C:\nonexistent",
            fileExists: _ => false,
            baseDirectory: @"C:\fallback\sdk\");

        // Non-existent DOTNET_ROOT is skipped; falls to baseDirectory parent
        Assert.Equal(@"C:\fallback", result);
    }

    [Fact]
    public void ResolveDotnetRoot_WalksUpFromProcessPath()
    {
        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: _ => null,
            processPath: @"C:\dotnet\sdk\11.0.100\dn.exe",
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: _ => false,
            fileExists: path => path == @"C:\dotnet\dotnet.exe",
            baseDirectory: @"C:\fallback\");

        Assert.Equal(@"C:\dotnet", result);
    }

    [Fact]
    public void ResolveDotnetRoot_NoDotnetAncestor_FallsBackToBaseDirectory()
    {
        string result = DotnetRootResolver.ResolveDotnetRoot(
            getEnvVar: _ => null,
            processPath: @"C:\app\bin\myapp.exe",
            processArch: Architecture.X64,
            isWindows: true,
            directoryExists: _ => false,
            fileExists: _ => false,
            baseDirectory: @"C:\fallback\sdk\");

        // Last resort: parent of baseDirectory
        Assert.Equal(@"C:\fallback", result);
    }

    [Fact]
    public void ResolveHostfxrPath_WithValidFxrDir_ReturnsPath()
    {
        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: @"C:\dotnet",
            isWindows: true,
            isMacOS: false,
            directoryExists: path => path == @"C:\dotnet\host\fxr",
            getDirectories: _ => new[] { @"C:\dotnet\host\fxr\9.0.0" },
            fileExists: path => path == @"C:\dotnet\host\fxr\9.0.0\hostfxr.dll");

        Assert.Equal(@"C:\dotnet\host\fxr\9.0.0\hostfxr.dll", result);
    }

    [Fact]
    public void ResolveHostfxrPath_PicksHighestVersion()
    {
        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: @"C:\dotnet",
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => new[]
            {
                @"C:\dotnet\host\fxr\8.0.0",
                @"C:\dotnet\host\fxr\9.0.1",
                @"C:\dotnet\host\fxr\9.0.0"
            },
            fileExists: _ => true);

        Assert.Equal(@"C:\dotnet\host\fxr\9.0.1\hostfxr.dll", result);
    }

    [Fact]
    public void ResolveHostfxrPath_SkipsPrereleaseDirectories()
    {
        // Version.TryParse fails for prerelease strings like "10.0.0-preview.5"
        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: @"C:\dotnet",
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => new[]
            {
                @"C:\dotnet\host\fxr\10.0.0-preview.5",
                @"C:\dotnet\host\fxr\9.0.0"
            },
            fileExists: _ => true);

        // Should pick 9.0.0 since the preview dir is skipped
        Assert.Equal(@"C:\dotnet\host\fxr\9.0.0\hostfxr.dll", result);
    }

    [Fact]
    public void ResolveHostfxrPath_MissingFxrDirectory_ReturnsEmpty()
    {
        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: @"C:\dotnet",
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
        string result = DotnetRootResolver.ResolveHostfxrPath(
            dotnetRoot: @"C:\dotnet",
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => new[] { @"C:\dotnet\host\fxr\9.0.0" },
            fileExists: _ => false);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveHostfxrPath_OnMacOS_LooksForDylib()
    {
        string dotnetRoot = "/usr/local/share/dotnet";
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
        string dotnetRoot = "/usr/share/dotnet";
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

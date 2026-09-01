// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Cli.Tests;

public class HostFxrLocatorTests
{
    private static string BuildPath(bool isWindows, params string[] segments)
    {
        string root = isWindows ? @"C:\" : "/";
        return segments.Length == 0 ? root : Path.Combine(root, Path.Combine(segments));
    }

    [Fact]
    public void ResolveHostFxrPath_WithValidFxrDir_ReturnsPath()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "11.0.0");
        string expectedPath = Path.Combine(fxrVersion, "hostfxr.dll");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: path => path == fxrDir,
            getDirectories: _ => [fxrVersion],
            fileExists: path => path == expectedPath);

        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveHostFxrPath_PicksHighestVersion()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string v800 = Path.Combine(fxrDir, "8.0.0");
        string v901 = Path.Combine(fxrDir, "9.0.1");
        string v900 = Path.Combine(fxrDir, "9.0.0");
        string expectedPath = Path.Combine(v901, "hostfxr.dll");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => [v800, v901, v900],
            fileExists: _ => true);

        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveHostFxrPath_NullOrEmptyDotnetRoot_ReturnsEmpty()
    {
        string resultNull = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: null,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => [],
            fileExists: _ => true);

        string resultEmpty = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: string.Empty,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => [],
            fileExists: _ => true);

        resultNull.Should().BeEmpty();
        resultEmpty.Should().BeEmpty();
    }

    [Fact]
    public void ResolveHostFxrPath_MissingFxrDirectory_ReturnsEmpty()
    {
        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: BuildPath(true, "dotnet"),
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => false,
            getDirectories: _ => [],
            fileExists: _ => false);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveHostFxrPath_FxrDirExistsButNoHostfxrFile_ReturnsEmpty()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "9.0.0");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => [fxrVersion],
            fileExists: _ => false);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveHostFxrPath_OnMacOS_LooksForDylib()
    {
        string dotnetRoot = Path.Combine("/", "usr", "local", "share", "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "9.0.0");
        string expectedPath = Path.Combine(fxrVersion, "libhostfxr.dylib");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: false,
            isMacOS: true,
            directoryExists: path => path == fxrDir,
            getDirectories: _ => [fxrVersion],
            fileExists: path => path == expectedPath);

        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveHostFxrPath_OnLinux_LooksForSo()
    {
        string dotnetRoot = Path.Combine("/", "usr", "share", "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "9.0.0");
        string expectedPath = Path.Combine(fxrVersion, "libhostfxr.so");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: false,
            isMacOS: false,
            directoryExists: path => path == fxrDir,
            getDirectories: _ => [fxrVersion],
            fileExists: path => path == expectedPath);

        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveHostFxrPath_FindsPrereleaseVersionDirectory()
    {
        string dotnetRoot = Path.Combine("/", "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string fxrVersion = Path.Combine(fxrDir, "11.0.0-preview.6.26359.118");
        string expectedPath = Path.Combine(fxrVersion, "libhostfxr.so");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: false,
            isMacOS: false,
            directoryExists: path => path == fxrDir,
            getDirectories: _ => [fxrVersion],
            fileExists: path => path == expectedPath);

        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveHostFxrPath_PicksHighestVersion_IncludingPrerelease()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string v900 = Path.Combine(fxrDir, "9.0.0");
        string v11Preview = Path.Combine(fxrDir, "11.0.0-preview.6.26359.118");
        string expectedPath = Path.Combine(v11Preview, "hostfxr.dll");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => [v900, v11Preview],
            fileExists: _ => true);

        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveHostFxrPath_PrefersStableOverPrereleaseOfSameCore()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string stable = Path.Combine(fxrDir, "11.0.0");
        string preview = Path.Combine(fxrDir, "11.0.0-preview.6.26359.118");
        string expectedPath = Path.Combine(stable, "hostfxr.dll");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => [preview, stable],
            fileExists: _ => true);

        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveHostFxrPath_OrdersPrereleaseSegmentsNumerically()
    {
        string dotnetRoot = BuildPath(true, "dotnet");
        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        string preview6 = Path.Combine(fxrDir, "11.0.0-preview.6.26359.118");
        string preview10 = Path.Combine(fxrDir, "11.0.0-preview.10.26400.1");
        string expectedPath = Path.Combine(preview10, "hostfxr.dll");

        string result = HostFxrLocator.ResolveHostFxrPath(
            dotnetRoot: dotnetRoot,
            isWindows: true,
            isMacOS: false,
            directoryExists: _ => true,
            getDirectories: _ => [preview6, preview10],
            fileExists: _ => true);

        result.Should().Be(expectedPath);
    }
}

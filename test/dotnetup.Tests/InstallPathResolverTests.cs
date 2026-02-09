// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for InstallPathResolver.
/// </summary>
public class InstallPathResolverTests(ITestOutputHelper output)
{
    private readonly InstallPathResolver _resolver = new(new DotnetInstallManager());
    
    // Use platform-appropriate temp paths for test data
    private static readonly string TempDir = Path.GetTempPath();
    private static readonly string ExplicitPath = Path.Combine(TempDir, "explicit-dotnet");
    private static readonly string GlobalJsonPath = Path.Combine(TempDir, "globaljson-dotnet");
    private static readonly string SamePath = Path.Combine(TempDir, "same-dotnet");

    /// <summary>
    /// Tests that explicit --install-path takes precedence over global.json's sdk-path,
    /// even when they differ. This is the key behavior change being tested.
    /// </summary>
    [Fact]
    public void Resolve_ExplicitPathOverridesGlobalJson()
    {
        var globalJsonInfo = CreateGlobalJsonInfo(GlobalJsonPath);

        var result = _resolver.Resolve(
            explicitInstallPath: ExplicitPath,
            globalJsonInfo: globalJsonInfo,
            currentDotnetInstallRoot: null,
            interactive: false,
            componentDescription: ".NET SDK",
            out string? error);

        output.WriteLine($"Error: {error ?? "(none)"}, Result: {result?.ResolvedInstallPath ?? "(null)"}");

        error.Should().BeNull("explicit install path should override global.json without error");
        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(ExplicitPath);
    }

    [Fact]
    public void Resolve_UsesGlobalJsonPath_WhenNoExplicitPath()
    {
        var globalJsonInfo = CreateGlobalJsonInfo(GlobalJsonPath);

        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: globalJsonInfo,
            currentDotnetInstallRoot: null,
            interactive: false,
            componentDescription: ".NET SDK",
            out string? error);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(GlobalJsonPath);
    }

    [Fact]
    public void Resolve_MatchingPathsSucceed()
    {
        var globalJsonInfo = CreateGlobalJsonInfo(SamePath);

        var result = _resolver.Resolve(
            explicitInstallPath: SamePath,
            globalJsonInfo: globalJsonInfo,
            currentDotnetInstallRoot: null,
            interactive: false,
            componentDescription: ".NET SDK",
            out string? error);

        error.Should().BeNull();
        result!.ResolvedInstallPath.Should().Be(SamePath);
    }

    [Fact]
    public void Resolve_UsesExplicitPath_WhenNoGlobalJson()
    {
        var result = _resolver.Resolve(
            explicitInstallPath: ExplicitPath,
            globalJsonInfo: null,
            currentDotnetInstallRoot: null,
            interactive: false,
            componentDescription: ".NET SDK",
            out string? error);

        error.Should().BeNull();
        result!.ResolvedInstallPath.Should().Be(ExplicitPath);
    }

    [Fact]
    public void Resolve_UsesDefaultPath_WhenNothingSpecified()
    {
        var installManager = new DotnetInstallManager();
        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: null,
            currentDotnetInstallRoot: null,
            interactive: false,
            componentDescription: ".NET SDK",
            out string? error);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(installManager.GetDefaultDotnetInstallPath());
    }

    [Fact]
    public void Resolve_UsesCurrentUserInstall_WhenNoExplicitOrGlobalJson()
    {
        var installRoot = new DotnetInstallRoot("/user/dotnet", InstallerUtilities.GetDefaultInstallArchitecture());
        var currentInstall = new DotnetInstallRootConfiguration(installRoot, InstallType.User, IsFullyConfigured: true);

        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: null,
            currentDotnetInstallRoot: currentInstall,
            interactive: false,
            componentDescription: ".NET SDK",
            out string? error);

        error.Should().BeNull();
        result!.ResolvedInstallPath.Should().Be("/user/dotnet");
    }

    private static GlobalJsonInfo CreateGlobalJsonInfo(string sdkPath)
    {
        // GlobalJsonInfo.SdkPath is computed from GlobalJsonContents.Sdk.Paths relative to GlobalJsonPath
        // We need a fully qualified path for the global.json directory
        string tempDir = Path.GetTempPath();
        string globalJsonPath = Path.Combine(tempDir, "global.json");

        return new GlobalJsonInfo
        {
            GlobalJsonPath = globalJsonPath,
            GlobalJsonContents = new GlobalJsonContents
            {
                Sdk = new GlobalJsonContents.SdkSection
                {
                    // Use the absolute path directly since it will be resolved relative to tempDir
                    Paths = [sdkPath]
                }
            }
        };
    }
}

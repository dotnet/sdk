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
            out string? error);

        output.WriteLine($"Error: {error ?? "(none)"}, Result: {result?.ResolvedInstallPath ?? "(null)"}");

        error.Should().BeNull("explicit install path should override global.json without error");
        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(ExplicitPath);
        result.PathSource.Should().Be(PathSource.Explicit);
    }

    [Fact]
    public void Resolve_UsesGlobalJsonPath_WhenNoExplicitPath()
    {
        var globalJsonInfo = CreateGlobalJsonInfo(GlobalJsonPath);

        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: globalJsonInfo,
            currentDotnetInstallRoot: null,
            out string? error);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(GlobalJsonPath);
        result.PathSource.Should().Be(PathSource.GlobalJson);
    }

    [Fact]
    public void Resolve_MatchingPathsSucceed()
    {
        var globalJsonInfo = CreateGlobalJsonInfo(SamePath);

        var result = _resolver.Resolve(
            explicitInstallPath: SamePath,
            globalJsonInfo: globalJsonInfo,
            currentDotnetInstallRoot: null,
            out string? error);

        error.Should().BeNull();
        result!.ResolvedInstallPath.Should().Be(SamePath);
        result.PathSource.Should().Be(PathSource.Explicit);
    }

    [Fact]
    public void Resolve_UsesExplicitPath_WhenNoGlobalJson()
    {
        var result = _resolver.Resolve(
            explicitInstallPath: ExplicitPath,
            globalJsonInfo: null,
            currentDotnetInstallRoot: null,
            out string? error);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(ExplicitPath);
        result.PathSource.Should().Be(PathSource.Explicit);
    }

    [Fact]
    public void Resolve_UsesDefaultPath_WhenNothingSpecified()
    {
        var installManager = new DotnetInstallManager();
        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: null,
            currentDotnetInstallRoot: null,
            out string? error);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(installManager.GetDefaultDotnetInstallPath());
        result.PathSource.Should().Be(PathSource.Default);
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
            out string? error);

        error.Should().BeNull();
        result!.ResolvedInstallPath.Should().Be("/user/dotnet");
        result.PathSource.Should().Be(PathSource.ExistingUserInstall);
    }

    /// <summary>
    /// Regression: before the refactor, passing an explicit path without a global.json
    /// caused the method to return null because all logic was gated behind the global.json check.
    /// </summary>
    [Fact]
    public void Resolve_ExplicitPath_WithoutGlobalJson_ReturnsNonNull()
    {
        var result = _resolver.Resolve(
            explicitInstallPath: ExplicitPath,
            globalJsonInfo: null,
            currentDotnetInstallRoot: null,
            out string? error);

        error.Should().BeNull();
        result.Should().NotBeNull("explicit path must work even without global.json");
        result!.ResolvedInstallPath.Should().Be(ExplicitPath);
        result.PathSource.Should().Be(PathSource.Explicit);
        result.InstallPathFromGlobalJson.Should().BeNull();
    }

    /// <summary>
    /// Explicit path should beat an existing user install.
    /// </summary>
    [Fact]
    public void Resolve_ExplicitPath_TakesPrecedenceOverExistingUserInstall()
    {
        var installRoot = new DotnetInstallRoot("/user/dotnet", InstallerUtilities.GetDefaultInstallArchitecture());
        var currentInstall = new DotnetInstallRootConfiguration(installRoot, InstallType.User, IsFullyConfigured: true);

        var result = _resolver.Resolve(
            explicitInstallPath: ExplicitPath,
            globalJsonInfo: null,
            currentDotnetInstallRoot: currentInstall,
            out string? error);

        error.Should().BeNull();
        result!.ResolvedInstallPath.Should().Be(ExplicitPath, "explicit path should win over existing user install");
        result.PathSource.Should().Be(PathSource.Explicit);
    }

    /// <summary>
    /// global.json path should beat an existing user install when no explicit path is given.
    /// </summary>
    [Fact]
    public void Resolve_GlobalJson_TakesPrecedenceOverExistingUserInstall()
    {
        var installRoot = new DotnetInstallRoot("/user/dotnet", InstallerUtilities.GetDefaultInstallArchitecture());
        var currentInstall = new DotnetInstallRootConfiguration(installRoot, InstallType.User, IsFullyConfigured: true);
        var globalJsonInfo = CreateGlobalJsonInfo(GlobalJsonPath);

        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: globalJsonInfo,
            currentDotnetInstallRoot: currentInstall,
            out string? error);

        error.Should().BeNull();
        result!.ResolvedInstallPath.Should().Be(GlobalJsonPath, "global.json should win over existing user install");
        result.PathSource.Should().Be(PathSource.GlobalJson);
    }

    /// <summary>
    /// Regression: without global.json, the default fallback must not return null.
    /// </summary>
    [Fact]
    public void Resolve_NoInputs_ReturnsDefaultPath_NotNull()
    {
        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: null,
            currentDotnetInstallRoot: null,
            out string? error);

        error.Should().BeNull();
        result.Should().NotBeNull("default path fallback must always produce a result");
        result!.ResolvedInstallPath.Should().NotBeNullOrEmpty();
        result.PathSource.Should().Be(PathSource.Default);
    }

    /// <summary>
    /// Admin installs should not be picked up — only User installs.
    /// </summary>
    [Fact]
    public void Resolve_AdminInstall_FallsToDefault_NotExistingInstall()
    {
        var installRoot = new DotnetInstallRoot("/admin/dotnet", InstallerUtilities.GetDefaultInstallArchitecture());
        var currentInstall = new DotnetInstallRootConfiguration(installRoot, InstallType.Admin, IsFullyConfigured: true);

        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: null,
            currentDotnetInstallRoot: currentInstall,
            out string? error);

        error.Should().BeNull();
        result!.ResolvedInstallPath.Should().NotBe("/admin/dotnet", "admin installs should not be used as fallback");
        result.PathSource.Should().Be(PathSource.Default);
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

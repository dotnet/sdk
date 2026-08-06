// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for InstallPathResolver.
/// </summary>
[TestClass]
public class InstallPathResolverTests
{
    private readonly ITestOutputHelper output;

    public InstallPathResolverTests(TestContext testContext)
    {
        output = new TestContextOutputHelper(testContext);
    }

    private readonly InstallPathResolver _resolver = new(new DotnetEnvironmentManager());

    // Use platform-appropriate temp paths for test data
    private static readonly string TempDir = Path.GetTempPath();
    private static readonly string ExplicitPath = Path.Combine(TempDir, "explicit-dotnet");
    private static readonly string GlobalJsonPath = Path.Combine(TempDir, "globaljson-dotnet");
    private static readonly string SamePath = Path.Combine(TempDir, "same-dotnet");

    /// <summary>
    /// Tests that explicit --install-path takes precedence over global.json's sdk-path,
    /// even when they differ. This is the key behavior change being tested.
    /// </summary>
    [TestMethod]
    public void Resolve_ExplicitPathOverridesGlobalJson()
    {
        var globalJsonInfo = CreateGlobalJsonInfo(GlobalJsonPath);

        var result = _resolver.Resolve(
            explicitInstallPath: ExplicitPath,
            globalJsonInfo: globalJsonInfo);

        output.WriteLine($"Result: {result?.ResolvedInstallPath ?? "(null)"}");

        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(ExplicitPath);
        result.PathSource.Should().Be(PathSource.Explicit);
    }

    [TestMethod]
    public void Resolve_UsesGlobalJsonPath_WhenNoExplicitPath()
    {
        var globalJsonInfo = CreateGlobalJsonInfo(GlobalJsonPath);

        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: globalJsonInfo);

        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(GlobalJsonPath);
        result.PathSource.Should().Be(PathSource.GlobalJson);
    }

    [TestMethod]
    public void Resolve_MatchingPathsSucceed()
    {
        var globalJsonInfo = CreateGlobalJsonInfo(SamePath);

        var result = _resolver.Resolve(
            explicitInstallPath: SamePath,
            globalJsonInfo: globalJsonInfo);

        result!.ResolvedInstallPath.Should().Be(SamePath);
        result.PathSource.Should().Be(PathSource.Explicit);
    }

    [TestMethod]
    public void Resolve_UsesExplicitPath_WhenNoGlobalJson()
    {
        var result = _resolver.Resolve(
            explicitInstallPath: ExplicitPath,
            globalJsonInfo: null);

        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(ExplicitPath);
        result.PathSource.Should().Be(PathSource.Explicit);
    }

    [TestMethod]
    public void Resolve_UsesDefaultPath_WhenNothingSpecified()
    {
        var installManager = new DotnetEnvironmentManager();
        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: null);

        result.Should().NotBeNull();
        result!.ResolvedInstallPath.Should().Be(installManager.GetDefaultDotnetInstallPath());
        result.PathSource.Should().Be(PathSource.Default);
    }

    /// <summary>
    /// Regression: before the refactor, passing an explicit path without a global.json
    /// caused the method to return null because all logic was gated behind the global.json check.
    /// </summary>
    [TestMethod]
    public void Resolve_ExplicitPath_WithoutGlobalJson_ReturnsNonNull()
    {
        var result = _resolver.Resolve(
            explicitInstallPath: ExplicitPath,
            globalJsonInfo: null);

        result.Should().NotBeNull("explicit path must work even without global.json");
        result!.ResolvedInstallPath.Should().Be(ExplicitPath);
        result.PathSource.Should().Be(PathSource.Explicit);
        result.InstallPathFromGlobalJson.Should().BeNull();
    }





    /// <summary>
    /// Regression: without global.json, the default fallback must not return null.
    /// </summary>
    [TestMethod]
    public void Resolve_NoInputs_ReturnsDefaultPath_NotNull()
    {
        var result = _resolver.Resolve(
            explicitInstallPath: null,
            globalJsonInfo: null);

        result.Should().NotBeNull("default path fallback must always produce a result");
        result!.ResolvedInstallPath.Should().NotBeNullOrEmpty();
        result.PathSource.Should().Be(PathSource.Default);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ResolveCurrentInstallRootPath_UsesSymlinkTargetDirectory()
    {
        using var testEnvironment = new TestEnvironment();
        string actualRoot = Path.Combine(testEnvironment.TempRoot, "usr", "lib", "dotnet");
        string binDir = Path.Combine(testEnvironment.TempRoot, "usr", "bin");
        Directory.CreateDirectory(actualRoot);
        Directory.CreateDirectory(binDir);

        string targetPath = Path.Combine(actualRoot, "dotnet");
        File.WriteAllText(targetPath, string.Empty);

        string linkPath = Path.Combine(binDir, "dotnet");
        File.CreateSymbolicLink(linkPath, targetPath);

        string resolvedRoot = DotnetEnvironmentManager.ResolveCurrentInstallRootPath(linkPath);

        resolvedRoot.Should().Be(actualRoot);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ResolveCurrentInstallRootPath_UsesRealDirectoryWhenParentDirectoryIsSymlinked()
    {
        using var testEnvironment = new TestEnvironment();
        string actualRoot = Path.Combine(testEnvironment.TempRoot, "usr", "lib", "dotnet");
        Directory.CreateDirectory(actualRoot);

        string targetPath = Path.Combine(actualRoot, "dotnet");
        File.WriteAllText(targetPath, string.Empty);

        string symlinkedRoot = Path.Combine(testEnvironment.TempRoot, "current-dotnet");
        Directory.CreateSymbolicLink(symlinkedRoot, actualRoot);

        string resolvedRoot = DotnetEnvironmentManager.ResolveCurrentInstallRootPath(Path.Combine(symlinkedRoot, "dotnet"));

        resolvedRoot.Should().Be(actualRoot);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ResolveRealPath_ResolvesSymlinkedDirectoryToItsRealPath()
    {
        // Mirrors a symlinked data directory (e.g. LocalApplicationData / XDG_DATA_HOME pointing
        // through a symlink): the resolved path must be the real directory so that a default install
        // path can be compared symmetrically against the realpath-resolved current install root.
        using var testEnvironment = new TestEnvironment();
        string actualDir = Path.Combine(testEnvironment.TempRoot, "real-data", "dotnet");
        Directory.CreateDirectory(actualDir);

        string symlinkedParent = Path.Combine(testEnvironment.TempRoot, "linked-data");
        Directory.CreateSymbolicLink(symlinkedParent, Path.Combine(testEnvironment.TempRoot, "real-data"));

        string resolved = ExecutablePathResolver.ResolveRealPath(Path.Combine(symlinkedParent, "dotnet"))!;

        resolved.Should().Be(actualDir);
    }

    [TestMethod]
    public void ResolveRealPath_ReturnsNull_WhenPathIsNullOrEmpty()
    {
        ExecutablePathResolver.ResolveRealPath(null).Should().BeNull();
        ExecutablePathResolver.ResolveRealPath(string.Empty).Should().BeNull();
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

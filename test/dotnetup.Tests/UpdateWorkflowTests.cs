// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class UpdateWorkflowTests
{
    [TestMethod]
    [DataRow(null, "10.0.1xx")]
    [DataRow("patch", "10.0.1xx")]
    [DataRow("feature", "10.0")]
    [DataRow("minor", "10")]
    [DataRow("major", "latest")]
    [DataRow("disable", "10.0.103")]
    public void GetChannelToUpdate_UsesGlobalJsonConstraintsInsteadOfCachedVersion(string? policy, string expectedChannel)
    {
        using var testEnv = new TestEnvironment();
        var path = Path.Combine(testEnv.TempRoot, "global.json");
        var policyProperty = policy is null ? "" : $""","rollForward":"{policy}" """;
        File.WriteAllText(path, $$$"""{"sdk":{"version":"10.0.103","allowPrerelease":false{{{policyProperty}}}}}""");
        var spec = RepositorySpec(path);
        spec.VersionOrChannel = "9.0.100";

        var channel = UpdateWorkflow.GetChannelToUpdate(spec)!;

        channel.Name.Should().Be(expectedChannel);
        channel.MinimumVersion!.ToString().Should().Be("10.0.103");
        channel.AllowPrerelease.Should().BeFalse();
        spec.VersionOrChannel.Should().Be("9.0.100");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("""{"sdk":{"rollForward":"latestPatch"}}""")]
    [DataRow("""{"sdk":{"rollForward":"disable"}}""")]
    public void UpdateRemovesInactiveRepositoryRequirementAndCollectsItsSdk(string? contents)
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);
        var path = Path.Combine(testEnv.TempRoot, "global.json");
        if (contents is not null)
        {
            File.WriteAllText(path, contents);
        }
        manifest.AddInstallSpec(root, RepositorySpec(path));
        Directory.CreateDirectory(Path.Combine(root.Path, "sdk", "10.0.103"));
        manifest.AddInstallation(root, new Installation
        {
            Component = InstallComponent.SDK, Version = "10.0.103", Subcomponents = ["sdk/10.0.103"]
        });

        new UpdateWorkflow(new ChannelVersionResolver())
            .Execute(testEnv.ManifestPath, root.Path, InstallComponent.SDK, noProgress: true);

        manifest.GetInstallSpecs(root).Should().BeEmpty();
        manifest.GetInstallations(root).Should().BeEmpty();
        Directory.Exists(Path.Combine(root.Path, "sdk", "10.0.103")).Should().BeFalse();
    }

    [TestMethod]
    public void InvalidRepositoryReportsFailureAfterOtherSpecsAreProcessed()
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallArchitecture.x64);
        var invalidPath = Path.Combine(testEnv.TempRoot, "invalid.json");
        File.WriteAllText(invalidPath, "{broken");
        manifest.AddInstallSpec(root, RepositorySpec(invalidPath));
        manifest.AddInstallSpec(root, RepositorySpec(Path.Combine(testEnv.TempRoot, "missing.json")));
        manifest.AddInstallSpec(root, new InstallSpec
        {
            Component = InstallComponent.SDK, InstallSource = InstallSource.Explicit, VersionOrChannel = "9.0.100"
        });

        var update = () => new UpdateWorkflow(new ChannelVersionResolver())
            .Execute(testEnv.ManifestPath, root.Path, InstallComponent.SDK, noProgress: true);
        update.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.ContextResolutionFailed);
        var remaining = manifest.GetInstallSpecs(root).ToList();
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(s => s.GlobalJsonPath == invalidPath);
        remaining.Should().Contain(s => s.InstallSource == InstallSource.Explicit);
    }

    private static InstallSpec RepositorySpec(string path) => new()
    {
        Component = InstallComponent.SDK,
        InstallSource = InstallSource.GlobalJson,
        VersionOrChannel = "10.0.1xx",
        GlobalJsonPath = path
    };
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Exercises the real CLI process against isolated manifests and fake disk installations.
/// Covers parsing, warnings, redirected confirmation, GC, and externally edited manifest state.
/// </summary>
[TestClass]
public class UninstallEndToEndTests : IDisposable
{
    private readonly UninstallTestEnvironment _fixture = new();

    [TestMethod]
    [DataRow("sdk", "11.0", InstallComponent.SDK, "11.0.100")]
    [DataRow("runtime", "11.0", InstallComponent.Runtime, "11.0.0")]
    [DataRow("runtime", "runtime@11.0", InstallComponent.Runtime, "11.0.0")]
    [DataRow("runtime", "aspnetcore@11.0", InstallComponent.ASPNETCore, "11.0.0")]
    [DataRow("runtime", "windowsdesktop@11.0", InstallComponent.WindowsDesktop, "11.0.0")]
    public void StraightforwardUninstall_DoesNotWarn(
        string noun, string request, InstallComponent component, string version)
    {
        _fixture.AddInstallation(component, version);
        _fixture.AddSpecs(component, "11.0");

        var output = Run([noun, "uninstall", request], out var exitCode, input: "");

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        output.Should().NotContain("will be removed from tracking");
        _fixture.ReadRoot().Installations.Should().BeEmpty();
        _fixture.ReadRoot().InstallSpecs.Should().BeEmpty();
        AssertFilesRemoved(component, version);
    }

    [TestMethod]
    public void OtherTrackedMajors_DoNotWarnAndRemainInstalled()
    {
        foreach (var major in new[] { 8, 9, 11 })
        {
            _fixture.AddInstallation(InstallComponent.SDK, $"{major}.0.100");
            _fixture.AddSpecs(InstallComponent.SDK, $"{major}.0");
        }

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode, input: "");

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        _fixture.ReadRoot().Installations.Select(i => i.Version).Should().BeEquivalentTo(["8.0.100", "9.0.100"]);
        _fixture.ReadRoot().InstallSpecs.Select(s => s.VersionOrChannel).Should().BeEquivalentTo(["8.0", "9.0"]);
        AssertFilesRemoved(InstallComponent.SDK, "11.0.100");
    }

    [TestMethod]
    [DataRow("y\n", true)]
    [DataRow("\n", true)]
    [DataRow("n\n", false)]
    [DataRow("\u001b\n", false)]
    [DataRow("", false)]
    [DataRow("invalid\nn\n", false)]
    public void GarbageCollectionWarning_ConfirmsBeforeMutation(string input, bool accept)
    {
        _fixture.AddInstallation(InstallComponent.SDK, "9.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0");
        var before = File.ReadAllText(_fixture.Environment.ManifestPath);

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode, input);

        exitCode.Should().Be(accept ? 0 : 1, output);
        AssertWarningCounts(output, retained: 0, unexpected: 1, prompts: 1);
        output.Should().Contain(".NET SDK 9.0.100 will be uninstalled");
        output.Should().Contain("Install spec '11.0' (.NET SDK; source: Explicit) will be removed from tracking.");
        output.Should().NotContain("retained versions");
        output.IndexOf("will be removed from tracking", StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("Proceed with uninstall?", StringComparison.Ordinal));
        if (accept)
        {
            _fixture.ReadRoot().Installations.Should().BeEmpty();
            AssertFilesRemoved(InstallComponent.SDK, "9.0.100");
            AssertFilesRemoved(InstallComponent.SDK, "11.0.100");
        }
        else
        {
            File.ReadAllText(_fixture.Environment.ManifestPath).Should().Be(before);
            _fixture.ReadRoot().Installations.Should().HaveCount(2);
            output.Should().Contain("Uninstall cancelled");
            output.Should().NotContain("Done.");
        }
    }

    [TestMethod]
    [DataRow("sdk", InstallComponent.SDK, "11.0.100", "9.0.100")]
    [DataRow("runtime", InstallComponent.Runtime, "11.0.0", "9.0.0")]
    public void SdkAndRuntime_GarbageCollectionWarningsHaveSameConfirmationBehavior(
        string noun, InstallComponent component, string target, string other)
    {
        _fixture.AddInstallation(component, target);
        _fixture.AddInstallation(component, other);
        _fixture.AddSpecs(component, "11.0");

        var output = Run([noun, "uninstall", "11.0"], out var exitCode);

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 0, unexpected: 1, prompts: 1);
        _fixture.ReadRoot().Installations.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("sdk")]
    [DataRow("runtime")]
    [DataRow("alias")]
    public void NonInteractive_OverridesInteractiveAndStillPrintsWarnings(string noun)
    {
        var component = noun == "runtime" ? InstallComponent.Runtime : InstallComponent.SDK;
        var version = noun == "runtime" ? "11.0.0" : "11.0.100";
        _fixture.AddInstallation(component, version);
        _fixture.AddSpecs(component, "11.0", "11");
        string[] command = noun == "alias" ? ["uninstall", "11.0"] : [noun, "uninstall", "11.0"];

        var output = Run(command, out var exitCode, input: "", "--non-interactive");

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 1, unexpected: 0, prompts: 0);
        _fixture.ReadRoot().Installations.Should().ContainSingle();
        _fixture.ReadRoot().InstallSpecs.Should().ContainSingle().Which.VersionOrChannel.Should().Be("11");
    }

    [TestMethod]
    [DataRow("11.0")]
    [DataRow("9.0")]
    [DataRow("11.0.100")]
    public void LatestSpec_DoesNotMatchDifferentStoredSpecName(string requested)
    {
        _fixture.AddInstallation(InstallComponent.SDK, "9.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "latest");
        var before = File.ReadAllText(_fixture.Environment.ManifestPath);

        var output = Run(["sdk", "uninstall", requested], out var exitCode);

        exitCode.Should().Be(1, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        output.Should().Contain("No");
        output.Should().Contain("install spec found");
        File.ReadAllText(_fixture.Environment.ManifestPath).Should().Be(before);
    }

    [TestMethod]
    public void MultipleUninstallArguments_AreRejectedWithoutChangingState()
    {
        _fixture.AddInstallation(InstallComponent.SDK, "9.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "9.0", "11.0", "latest");
        var before = File.ReadAllText(_fixture.Environment.ManifestPath);

        var output = Run(["sdk", "uninstall", "11.0", "9.0"], out var exitCode);

        exitCode.Should().Be(1, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        output.Should().Contain("9.0");
        File.ReadAllText(_fixture.Environment.ManifestPath).Should().Be(before);
    }

    [TestMethod]
    public void UninstallLatest_RemovesOlderAndNewerUnreferencedStableVersions()
    {
        _fixture.AddInstallation(InstallComponent.SDK, "9.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "latest");

        var output = Run(["sdk", "uninstall", "latest"], out var exitCode, input: "");

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        _fixture.ReadRoot().Installations.Should().BeEmpty();
        _fixture.ReadRoot().InstallSpecs.Should().BeEmpty();
    }

    [TestMethod]
    public void OtherRuntimeComponentsRetainedByMultipleSpecs_DoNotWarn()
    {
        _fixture.AddInstallation(InstallComponent.Runtime, "11.0.0");
        _fixture.AddSpecs(InstallComponent.Runtime, "11.0");
        foreach (var component in new[] { InstallComponent.ASPNETCore, InstallComponent.WindowsDesktop })
        {
            _fixture.AddInstallation(component, "11.0.0");
            _fixture.AddSpecs(component, "11", "11.0.0");
        }

        var output = Run(["runtime", "uninstall", "runtime@11.0"], out var exitCode, input: "");

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        _fixture.ReadRoot().Installations.Select(i => i.Component)
            .Should().BeEquivalentTo([InstallComponent.ASPNETCore, InstallComponent.WindowsDesktop]);
        AssertFilesRemoved(InstallComponent.Runtime, "11.0.0");
    }

    [TestMethod]
    [DataRow("sdk", InstallComponent.SDK, "11.0.100", InstallComponent.Runtime, "11.0.0")]
    [DataRow("runtime", InstallComponent.Runtime, "11.0.0", InstallComponent.SDK, "11.0.100")]
    public void UnreferencedOtherComponentWithSameMajor_WarnsAboutActualRemoval(
        string noun, InstallComponent component, string target, InstallComponent otherComponent, string other)
    {
        _fixture.AddInstallation(component, target);
        _fixture.AddInstallation(otherComponent, other);
        _fixture.AddSpecs(component, "11.0");

        var output = Run([noun, "uninstall", "11.0"], out var exitCode);

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 0, unexpected: 1, prompts: 1);
        output.Should().Contain($"{otherComponent.GetDisplayName()} {other} will be uninstalled");
        _fixture.ReadRoot().Installations.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("11.0@core")]
    [DataRow("core@11.0")]
    public void UnsupportedRuntimeComponentSyntax_IsRejectedWithoutMutation(string request)
    {
        _fixture.AddInstallation(InstallComponent.Runtime, "11.0.0");
        _fixture.AddSpecs(InstallComponent.Runtime, "11.0");
        var before = File.ReadAllText(_fixture.Environment.ManifestPath);

        var output = Run(["runtime", "uninstall", request], out var exitCode, input: "");

        exitCode.Should().Be(1, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        File.ReadAllText(_fixture.Environment.ManifestPath).Should().Be(before);
    }

    [TestMethod]
    public void RequestedChannelAbsent_OverlappingSpecsDoNotGetRemoved()
    {
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "11", "11.0.1xx");
        var before = File.ReadAllText(_fixture.Environment.ManifestPath);

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode);

        exitCode.Should().Be(1, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        File.ReadAllText(_fixture.Environment.ManifestPath).Should().Be(before);
    }

    [TestMethod]
    [DataRow("sdk", "11.0", InstallComponent.SDK, "11.0.100", "11.0.1xx")]
    [DataRow("sdk", "11.0.100", InstallComponent.SDK, "11.0.100", "11.0.1xx")]
    [DataRow("runtime", "11.0", InstallComponent.Runtime, "11.0.0", "11.0.0")]
    public void SharedTarget_ListsBothRemainingSpecsAndKeepsFiles(
        string noun, string request, InstallComponent component, string version, string otherSpec)
    {
        _fixture.AddInstallation(component, version);
        _fixture.AddSpecs(component, request, "11", otherSpec);

        var output = Run([noun, "uninstall", request], out var exitCode);

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 1, unexpected: 0, prompts: 1);
        output.Should().Contain($"{component.GetDisplayName()} 11 (source: Explicit)");
        output.Should().Contain($"{component.GetDisplayName()} {otherSpec} (source: Explicit)");
        Count(output, "The retained versions listed above will remain installed.").Should().Be(1);
        var removalMessage = $"Install spec '{request}' ({component.GetDisplayName()}; source: Explicit) will be removed from tracking.";
        Count(output, removalMessage).Should().Be(1);
        output.IndexOf(removalMessage, StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("Proceed with uninstall?", StringComparison.Ordinal));
        _fixture.ReadRoot().Installations.Should().ContainSingle();
        _fixture.ReadRoot().InstallSpecs.Select(s => s.VersionOrChannel).Should().BeEquivalentTo(["11", otherSpec]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OlderAndNewerPatchesWithSameChannel_BothRemovedWithoutWarning(bool duplicateSpec)
    {
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.104");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0");
        if (duplicateSpec)
        {
            _fixture.AlterManifest(root => root.InstallSpecs.Add(new InstallSpec
            {
                Component = InstallComponent.SDK, VersionOrChannel = "11.0", InstallSource = InstallSource.Explicit
            }));
        }

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode, input: "");

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        Count(output, "Dereferenced .NET SDK 11.0").Should().Be(1);
        _fixture.ReadRoot().Installations.Should().BeEmpty();
        _fixture.ReadRoot().InstallSpecs.Should().BeEmpty();
        AssertFilesRemoved(InstallComponent.SDK, "11.0.100");
        AssertFilesRemoved(InstallComponent.SDK, "11.0.104");
    }

    [TestMethod]
    public void LatestKeepsOnlyNewerPatch_OnlyNewerPatchHasRetentionWarning()
    {
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.104");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0", "latest");

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode);

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 1, unexpected: 0, prompts: 1);
        output.Should().Contain("11.0.104 will not be uninstalled");
        output.Should().NotContain("11.0.100 will not be uninstalled");
        _fixture.ReadRoot().Installations.Should().ContainSingle().Which.Version.Should().Be("11.0.104");
        AssertFilesRemoved(InstallComponent.SDK, "11.0.100");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ChannelTargetsMultipleSharedVersions_OnePromptAndOneWarningPerVersion(bool accept)
    {
        _fixture.AddInstallation(InstallComponent.SDK, "9.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.104");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0", "11.0.100", "11.0.104");
        var before = File.ReadAllText(_fixture.Environment.ManifestPath);

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode, accept ? "y\n" : "n\n");

        exitCode.Should().Be(accept ? 0 : 1, output);
        AssertWarningCounts(output, retained: 2, unexpected: 1, prompts: 1);
        if (accept)
        {
            _fixture.ReadRoot().Installations.Select(i => i.Version).Should().BeEquivalentTo(["11.0.100", "11.0.104"]);
            _fixture.ReadRoot().InstallSpecs.Select(s => s.VersionOrChannel).Should().BeEquivalentTo(["11.0.100", "11.0.104"]);
            AssertFilesRemoved(InstallComponent.SDK, "9.0.100");
        }
        else
        {
            File.ReadAllText(_fixture.Environment.ManifestPath).Should().Be(before);
        }
    }

    [TestMethod]
    public void RefreshedGlobalJson_KeepsTargetAndListsItsPath()
    {
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0");
        var directory = Path.Combine(_fixture.Environment.TempRoot, "[repository]");
        Directory.CreateDirectory(directory);
        var globalJson = Path.Combine(directory, "global.json");
        File.WriteAllText(globalJson, """{"sdk":{"version":"11.0.100"}}""");
        _fixture.AlterManifest(root => root.InstallSpecs.Add(new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "9.0.1xx",
            InstallSource = InstallSource.GlobalJson, GlobalJsonPath = globalJson
        }));

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode);

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 1, unexpected: 0, prompts: 1);
        output.Should().Contain($".NET SDK 11.0.1xx (source: {globalJson})");
        _fixture.ReadRoot().Installations.Should().ContainSingle();
        _fixture.ReadRoot().InstallSpecs.Should().ContainSingle().Which.VersionOrChannel.Should().Be("11.0.1xx");
    }

    [TestMethod]
    public void SdkSpecKeepsRuntimeFiles_WarnsThatRuntimeWillRemainInstalled()
    {
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100", "shared/Microsoft.NETCore.App/11.0.0");
        _fixture.AddInstallation(InstallComponent.Runtime, "11.0.0");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0");
        _fixture.AddSpecs(InstallComponent.Runtime, "11.0");

        var output = Run(["runtime", "uninstall", "11.0"], out var exitCode);

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 1, unexpected: 0, prompts: 1);
        output.Should().Contain("dotnet (runtime) 11.0.0 will not be uninstalled");
        output.Should().Contain(".NET SDK 11.0 (source: Explicit)");
        Directory.Exists(Path.Combine(_fixture.Environment.InstallPath, "shared", "Microsoft.NETCore.App", "11.0.0"))
            .Should().BeTrue();
    }

    [TestMethod]
    public void DuplicateManifestEntries_DoNotDuplicateWarningsOrSpecMessages()
    {
        _fixture.AddInstallation(InstallComponent.SDK, "9.0.100");
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0", "latest");
        _fixture.AlterManifest(root =>
        {
            root.Installations.Add(root.Installations.Single(i => i.Version == "9.0.100"));
            root.Installations.Add(root.Installations.Single(i => i.Version == "11.0.100"));
            root.InstallSpecs.Add(new InstallSpec
            {
                Component = InstallComponent.SDK, VersionOrChannel = "latest", InstallSource = InstallSource.Explicit
            });
        });

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode);

        exitCode.Should().Be(0, output);
        AssertWarningCounts(output, retained: 1, unexpected: 1, prompts: 1);
        Count(output, ".NET SDK latest (source: Explicit)").Should().Be(1);
        Count(output, "Removed sdk/9.0.100").Should().Be(1);
    }

    [TestMethod]
    public void InvalidExternallyEditedVersion_FailsWithoutDeletingAnything()
    {
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0");
        _fixture.AlterManifest(root => root.Installations[0].Version = "invalid");
        var before = File.ReadAllText(_fixture.Environment.ManifestPath);

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode);

        exitCode.Should().Be(1, output);
        AssertWarningCounts(output, retained: 0, unexpected: 0, prompts: 0);
        output.Should().Contain("modified outside of dotnetup");
        File.ReadAllText(_fixture.Environment.ManifestPath).Should().Be(before);
        Directory.Exists(Path.Combine(_fixture.Environment.InstallPath, "sdk", "11.0.100")).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void SourceAll_ListsEachSpecBeforeConfirmationWithoutDeletingGlobalJson(bool accept)
    {
        _fixture.AddInstallation(InstallComponent.SDK, "11.0.100");
        _fixture.AddSpecs(InstallComponent.SDK, "11.0", "11");
        var directory = Path.Combine(_fixture.Environment.TempRoot, "[repository]");
        Directory.CreateDirectory(directory);
        var globalJson = Path.Combine(directory, "global.json");
        const string GlobalJsonContents = """{"sdk":{"version":"11.0.100"}}""";
        File.WriteAllText(globalJson, GlobalJsonContents);
        _fixture.AlterManifest(root => root.InstallSpecs.Add(new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "11.0",
            InstallSource = InstallSource.GlobalJson, GlobalJsonPath = globalJson
        }));
        var before = File.ReadAllText(_fixture.Environment.ManifestPath);

        var output = Run(["sdk", "uninstall", "11.0"], out var exitCode,
            accept ? "y\n" : "n\n", "--source", "all");

        exitCode.Should().Be(accept ? 0 : 1, output);
        AssertWarningCounts(output, retained: 1, unexpected: 0, prompts: 1);
        Count(output, "will be removed from tracking.").Should().Be(2);
        var explicitMessage = "Install spec '11.0' (.NET SDK; source: Explicit) will be removed from tracking.";
        var globalJsonMessage = $"Install spec '11.0' (.NET SDK; source: {globalJson}) will be removed from tracking.";
        output.Should().Contain(explicitMessage).And.Contain(globalJsonMessage);
        output.IndexOf(explicitMessage, StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("Proceed with uninstall?", StringComparison.Ordinal));
        output.IndexOf(globalJsonMessage, StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("Proceed with uninstall?", StringComparison.Ordinal));
        File.ReadAllText(globalJson).Should().Be(GlobalJsonContents);
        if (accept)
        {
            _fixture.ReadRoot().InstallSpecs.Should().ContainSingle().Which.VersionOrChannel.Should().Be("11");
        }
        else
        {
            File.ReadAllText(_fixture.Environment.ManifestPath).Should().Be(before);
        }
    }

    private string Run(string[] command, out int exitCode, string input = "y\n", params string[] options)
    {
        var environment = _fixture.Environment;
        string[] arguments =
        [
            .. command, "--manifest-path", environment.ManifestPath,
            "--install-path", environment.InstallPath, "--interactive", "true", .. options
        ];
        var result = DotnetupTestUtilities.RunDotnetupProcess(
            arguments, captureOutput: true, workingDirectory: environment.TempRoot,
            environmentVariables: new Dictionary<string, string>
            {
                ["DOTNET_TESTHOOK_DOTNETUP_DATA_DIR"] = environment.TempRoot,
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
            },
            standardInput: input, timeoutMilliseconds: 60_000);
        exitCode = result.exitCode;
        return Regex.Replace(result.output, @"\s+", " ");
    }

    private static void AssertWarningCounts(string output, int retained, int unexpected, int prompts)
    {
        Count(output, "will not be uninstalled").Should().Be(retained, output);
        Count(output, "will be uninstalled").Should().Be(unexpected, output);
        Count(output, "Proceed with uninstall?").Should().Be(prompts, output);
    }

    private static int Count(string output, string message) => Regex.Matches(output, Regex.Escape(message)).Count;

    private void AssertFilesRemoved(InstallComponent component, string version)
    {
        var path = component == InstallComponent.SDK
            ? Path.Combine(_fixture.Environment.InstallPath, "sdk", version)
            : Path.Combine(_fixture.Environment.InstallPath, "shared", component.GetFrameworkName(), version);
        Directory.Exists(path).Should().BeFalse(path);
    }

    public void Dispose() => _fixture.Dispose();
}

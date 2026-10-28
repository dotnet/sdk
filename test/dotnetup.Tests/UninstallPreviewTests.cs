// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Verifies preview messages against prepared GC snapshots without modifying installation state.
/// </summary>
[TestClass]
public class UninstallPreviewTests
{
    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_RetainedTarget_ListsEveryResolvingSpecAndGlobalJsonPath()
    {
        var target = new Installation { Component = InstallComponent.SDK, Version = "8.0.100" };
        var globalJson = Path.Combine(Path.GetTempPath(), "[repository]", "global.json");
        var specs = new List<InstallSpec>
        {
            new() { Component = InstallComponent.SDK, VersionOrChannel = "8.0.100", InstallSource = InstallSource.Explicit },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "8.0.1xx", InstallSource = InstallSource.GlobalJson, GlobalJsonPath = globalJson }
        };
        var plan = CreatePlan(new Dictionary<Installation, List<InstallSpec>> { [target] = specs }, [], []);

        var output = Render(plan, [target], InstallComponent.SDK, "8", out var warned);

        warned.Should().BeTrue();
        output.Should().Contain(".NET SDK 8.0.100 will not be uninstalled");
        output.Should().Contain("8.0.100 (source: Explicit)");
        output.Should().Contain("8.0.1xx");
        output.Should().Contain(globalJson);
        output.Should().NotContain("The retained versions listed above");
        output.Should().Contain("Install spec '8' (.NET SDK; source: Explicit) will be removed from tracking.");
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_OnlyLatestMatchingVersionHasReferences()
    {
        var old = new Installation { Component = InstallComponent.SDK, Version = "8.0.100" };
        var latest = new Installation { Component = InstallComponent.SDK, Version = "8.0.101" };
        var spec = new InstallSpec { Component = InstallComponent.SDK, VersionOrChannel = "8" };
        var plan = CreatePlan(new Dictionary<Installation, List<InstallSpec>> { [latest] = [spec] }, [old], ["sdk/8.0.100"]);

        var output = Render(plan, [old, latest], InstallComponent.SDK, "8", out var warned);

        warned.Should().BeTrue();
        output.Should().Contain("8.0.101 will not be uninstalled");
        output.Should().NotContain("8.0.100 will not be uninstalled");
        output.Should().NotContain("will be uninstalled");
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    [DataRow("sdk/9.0.100", true)]
    [DataRow("sdk/8.0.100", false)]
    [DataRow("shared/Microsoft.AspNetCore.App/8.0.0", true)]
    public void Display_OrphanDirectories_WarnsOnlyOutsideRequestedComponentAndChannel(string path, bool expectedWarning)
    {
        var plan = CreatePlan([], [], [path]);

        var output = Render(plan, [], InstallComponent.SDK, "8", out var warned);

        warned.Should().Be(expectedWarning);
        if (expectedWarning)
        {
            output.Should().Contain("will be uninstalled because it is no longer referenced by any install specs.");
        }
        else
        {
            output.Should().BeEmpty();
        }
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_RuntimeFilesKeptBySdk_WarnsAboutRetentionNotRemoval()
    {
        var runtime = new Installation { Component = InstallComponent.Runtime, Version = "8.0.0" };
        var sdk = new Installation
        {
            Component = InstallComponent.SDK, Version = "8.0.100",
            Subcomponents = ["sdk/8.0.100", "shared/Microsoft.NETCore.App/8.0.0"]
        };
        var sdkSpec = new InstallSpec { Component = InstallComponent.SDK, VersionOrChannel = "8" };
        var plan = CreatePlan(new Dictionary<Installation, List<InstallSpec>> { [sdk] = [sdkSpec] }, [runtime], []);

        var output = Render(plan, [runtime], InstallComponent.Runtime, "8", out var warned);

        warned.Should().BeTrue();
        output.Should().Contain("dotnet (runtime) 8.0.0 will not be uninstalled");
        output.Should().Contain(".NET SDK 8");
        output.Should().NotContain("will be uninstalled");
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_RemovedRuntimeRecordWithSharedFiles_DoesNotClaimPhysicalRemoval()
    {
        var runtime = new Installation { Component = InstallComponent.Runtime, Version = "8.0.0" };
        var plan = CreatePlan([], [runtime], []);

        var output = Render(plan, [], InstallComponent.SDK, "9", out var warned);

        warned.Should().BeFalse();
        output.Should().BeEmpty();
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_BundledRuntimeRemoval_IsPartOfRequestedSdk()
    {
        var sdk = new Installation
        {
            Component = InstallComponent.SDK, Version = "8.0.100",
            Subcomponents = ["sdk/8.0.100", "shared/Microsoft.NETCore.App/8.0.0"]
        };
        var plan = CreatePlan([], [sdk], sdk.Subcomponents);

        var output = Render(plan, [sdk], InstallComponent.SDK, "8", out var warned);

        warned.Should().BeFalse();
        output.Should().BeEmpty();
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_RemovalSummary_DistinguishesSourcesAndDeduplicatesSpecs()
    {
        var globalJson = Path.Combine(Path.GetTempPath(), "[repository]", "global.json");
        var explicitSpec = new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "8", InstallSource = InstallSource.Explicit
        };
        var globalJsonSpec = new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "8",
            InstallSource = InstallSource.GlobalJson, GlobalJsonPath = globalJson
        };
        var plan = CreatePlan([], [], ["sdk/9.0.100"]);

        var output = Render(plan, [], InstallComponent.SDK, "8", out var warned,
            [explicitSpec, globalJsonSpec, explicitSpec]);

        warned.Should().BeTrue();
        output.Split("will be removed from tracking.").Should().HaveCount(3);
        output.Should().Contain("Install spec '8' (.NET SDK; source: Explicit) will be removed from tracking.");
        output.Should().Contain($"Install spec '8' (.NET SDK; source: {globalJson}) will be removed from tracking.");
        output.Should().NotContain("retained versions");
        output.IndexOf("will be uninstalled", StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("Install spec", StringComparison.Ordinal));
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    [DataRow("8[red]")]
    [DataRow("8{0}")]
    public void Display_RemovalSummary_EscapesSpecValuesInResourceFormat(string channel)
    {
        var plan = CreatePlan([], [], ["sdk/9.0.100"]);

        var output = Render(plan, [], InstallComponent.SDK, "8", out var warned,
            [new InstallSpec { Component = InstallComponent.SDK, VersionOrChannel = channel }]);

        warned.Should().BeTrue();
        output.Should().Contain($"Install spec '{channel}' (.NET SDK; source: Explicit) will be removed from tracking.");
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    [DataRow(InstallComponent.SDK, true)]
    [DataRow(InstallComponent.SDK, false)]
    [DataRow(InstallComponent.Runtime, true)]
    [DataRow(InstallComponent.Runtime, false)]
    public void Display_UnexpectedRemoval_SaysAlsoOnlyWhenRequestedFilesAreRemoved(
        InstallComponent component, bool removesRequestedFiles)
    {
        var target = new Installation
        {
            Component = component, Version = component == InstallComponent.SDK ? "8.0.100" : "8.0.0"
        };
        var unexpected = new Installation { Component = InstallComponent.SDK, Version = "9.0.100" };
        var removals = new List<Installation> { unexpected };
        var paths = new List<string> { "sdk/9.0.100" };
        var installSpecsByInstallation = new Dictionary<Installation, List<InstallSpec>>();
        if (removesRequestedFiles)
        {
            removals.Add(target);
            paths.Add(component == InstallComponent.SDK ? "sdk/8.0.100" : "shared/Microsoft.NETCore.App/8.0.0");
        }
        else
        {
            installSpecsByInstallation[target] = [new InstallSpec { Component = component, VersionOrChannel = "8.0" }];
        }

        var output = Render(CreatePlan(installSpecsByInstallation, removals, paths), [target], component, "8", out var warned);

        warned.Should().BeTrue();
        output.Should().Contain(removesRequestedFiles
            ? ".NET SDK 9.0.100 will also be uninstalled"
            : ".NET SDK 9.0.100 will be uninstalled");
        output.Should().NotContain("The retained versions listed above");
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_UntrackedRequestedFilesRemoved_SaysAlsoForOtherOrphans()
    {
        var plan = CreatePlan([], [], ["sdk/8.0.100", "sdk/9.0.100"]);

        var output = Render(plan, [], InstallComponent.SDK, "8", out var warned);

        warned.Should().BeTrue();
        output.Should().Contain(".NET SDK 9.0.100 will also be uninstalled");
        output.Should().NotContain(".NET SDK 8.0.100 will");
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_RuntimeRecordRemovedButFilesKeptBySdk_DoesNotSayAlso()
    {
        var runtime = new Installation { Component = InstallComponent.Runtime, Version = "8.0.0" };
        var sdk = new Installation
        {
            Component = InstallComponent.SDK, Version = "8.0.100",
            Subcomponents = ["sdk/8.0.100", "shared/Microsoft.NETCore.App/8.0.0"]
        };
        var sdkSpec = new InstallSpec { Component = InstallComponent.SDK, VersionOrChannel = "8" };
        var plan = CreatePlan(new Dictionary<Installation, List<InstallSpec>> { [sdk] = [sdkSpec] },
            [runtime], ["sdk/9.0.100"]);

        var output = Render(plan, [runtime], InstallComponent.Runtime, "8", out var warned);

        warned.Should().BeTrue();
        output.Should().Contain("dotnet (runtime) 8.0.0 will not be uninstalled");
        output.Should().Contain(".NET SDK 9.0.100 will be uninstalled");
        output.Should().NotContain("will also be uninstalled");
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.Console)]
    public void Display_UntrackedRequestedRuntimeFilesRemovedWithOrphanSdk_SaysAlso()
    {
        var sdk = new Installation
        {
            Component = InstallComponent.SDK, Version = "9.0.100",
            Subcomponents = ["sdk/9.0.100", "shared/Microsoft.NETCore.App/8.0.0"]
        };
        var plan = CreatePlan([], [sdk], sdk.Subcomponents);

        var output = Render(plan, [], InstallComponent.Runtime, "8", out var warned);

        warned.Should().BeTrue();
        output.Should().Contain(".NET SDK 9.0.100 will also be uninstalled");
    }

    private static GarbageCollectionPlan CreatePlan(
        Dictionary<Installation, List<InstallSpec>> installSpecsByInstallation, List<Installation> removals, List<string> paths)
    {
        var root = new DotnetRootEntry { Path = Path.GetTempPath() };
        return new GarbageCollectionPlan(new DotnetupManifestData { DotnetRoots = [root] }, root, installSpecsByInstallation, removals, paths);
    }

    private static string Render(
        GarbageCollectionPlan plan, List<Installation> targets, InstallComponent component, string channel, out bool warned,
        List<InstallSpec>? specsToRemove = null)
    {
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        using var writer = new StringWriter();
        var original = AnsiConsole.Console;
        try
        {
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(writer)
            });
            console.Profile.Width = 240;
            AnsiConsole.Console = console;
            specsToRemove ??= [new InstallSpec { Component = component, VersionOrChannel = channel, InstallSource = InstallSource.Explicit }];
            warned = UninstallPreview.Display(plan, targets, component, channel, specsToRemove);
            return ConsoleOutputNormalizer.StripAnsi(writer.ToString());
        }
        finally
        {
            AnsiConsole.Console = original;
        }
    }
}

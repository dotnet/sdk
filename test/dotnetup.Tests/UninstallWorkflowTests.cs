// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class UninstallWorkflowTests
{
    private const string DefaultUserPath = "/home/user/.dotnet";
    private const string AdminPath = "/usr/share/dotnet";
    private const string ExplicitPath = "/custom/dotnet";

    [TestMethod]
    [DataRow(InstallComponent.SDK)]
    [DataRow(InstallComponent.Runtime)]
    public void Uninstall_RemovesStandaloneSpecsButPreservesRepositoryRequirements(InstallComponent component)
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        foreach (var source in new[] { InstallSource.Explicit, InstallSource.Migration })
        {
            manifest.AddInstallSpec(root, new InstallSpec
            {
                Component = component, VersionOrChannel = "10.0", InstallSource = source
            });
        }
        manifest.AddInstallSpec(root, new InstallSpec
        {
            Component = component, VersionOrChannel = "9.0", InstallSource = InstallSource.Explicit
        });
        var subcomponent = component == InstallComponent.SDK ? "sdk/10.0.100" : "shared/Microsoft.NETCore.App/10.0.100";
        testEnv.StubComponentDirectories(null, (component, "10.0.100"));
        manifest.AddInstallation(root, new Installation
        {
            Component = component, Version = "10.0.100", Subcomponents = [subcomponent]
        });
        if (component == InstallComponent.SDK)
        {
            var globalJsonPath = Path.Combine(testEnv.TempRoot, "global.json");
            File.WriteAllText(globalJsonPath, """{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}""");
            manifest.AddInstallSpec(root, new InstallSpec
            {
                Component = component, VersionOrChannel = "10.0",
                InstallSource = InstallSource.GlobalJson, GlobalJsonPath = globalJsonPath
            });
        }

        UninstallWorkflow.Execute(testEnv.ManifestPath, testEnv.InstallPath, "10.0", component);

        var remaining = manifest.GetInstallSpecs(root).ToList();
        remaining.Should().NotContain(s => s.InstallSource == InstallSource.Migration);
        remaining.Where(s => s.InstallSource == InstallSource.Explicit).Should()
            .ContainSingle().Which.VersionOrChannel.Should().Be("9.0");
        remaining.Count(s => s.InstallSource == InstallSource.GlobalJson).Should().Be(component == InstallComponent.SDK ? 1 : 0);
        manifest.GetInstallations(root).Count().Should().Be(component == InstallComponent.SDK ? 1 : 0);
        Directory.Exists(Path.Combine(testEnv.InstallPath, subcomponent)).Should().Be(component == InstallComponent.SDK);
    }

    [TestMethod]
    [DataRow(new[] { "uninstall", "10.0" })]
    [DataRow(new[] { "sdk", "uninstall", "10.0" })]
    [DataRow(new[] { "runtime", "uninstall", "10.0" })]
    public void UninstallParser_RejectsSourceOption(string[] args)
    {
        Parser.Parse(args).Errors.Should().BeEmpty();
        foreach (var source in new[] { "explicit", "commandLine", "globaljson", "migration", "all" })
        {
            var result = Parser.Parse([.. args, "--source", source]);
            result.Errors.Should().Contain(e => e.Message.Contains("--source", StringComparison.Ordinal));
        }
        Parser.Parse(args).CommandResult.Command.Options.Should().NotContain(o => o.Name == "--source");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Uninstall_NoStandaloneSpecDoesNotRemoveRepositoryRequirement(bool matchingChannel)
    {
        using var testEnv = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(testEnv.ManifestPath);
        var root = new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture());
        string globalJsonPath = Path.Combine(testEnv.TempRoot, "global.json");
        const string contents = """{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}""";
        File.WriteAllText(globalJsonPath, contents);
        manifest.AddInstallSpec(root, new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "10.0",
            InstallSource = InstallSource.GlobalJson, GlobalJsonPath = globalJsonPath
        });
        testEnv.StubComponentDirectories(null, (InstallComponent.SDK, "10.0.100"));
        manifest.AddInstallation(root, new Installation
        {
            Component = InstallComponent.SDK, Version = "10.0.100", Subcomponents = ["sdk/10.0.100"]
        });

        var act = () => UninstallWorkflow.Execute(testEnv.ManifestPath, testEnv.InstallPath,
            matchingChannel ? "10.0" : "9.0", InstallComponent.SDK);

        act.Should().Throw<DotnetInstallException>().Where(e => e.ErrorCode == DotnetInstallErrorCode.UninstallTargetNotFound);
        manifest.GetInstallSpecs(root).Should().ContainSingle().Which.InstallSource.Should().Be(InstallSource.GlobalJson);
        manifest.GetInstallations(root).Should().ContainSingle();
        Directory.Exists(Path.Combine(testEnv.InstallPath, "sdk", "10.0.100")).Should().BeTrue();
        File.ReadAllText(globalJsonPath).Should().Be(contents);
    }

    [TestMethod]
    [DataRow(InstallComponent.SDK, "8.0.100")]
    [DataRow(InstallComponent.Runtime, "8.0.0")]
    [DataRow(InstallComponent.ASPNETCore, "8.0.0")]
    [DataRow(InstallComponent.WindowsDesktop, "8.0.0")]
    public void Execute_SharedTarget_DeclinePreservesManifestAndFiles(InstallComponent component, string version)
    {
        using var environment = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(environment.ManifestPath);
        var root = new DotnetInstallRoot(environment.InstallPath, InstallArchitecture.x64);
        AddInstallation(environment, manifest, root, component, version);
        AddSpec(manifest, root, component, "8");
        AddSpec(manifest, root, component, version);
        var before = File.ReadAllText(environment.ManifestPath);
        var prompted = false;

        Action uninstall = () => UninstallWorkflow.Execute(
            environment.ManifestPath, environment.InstallPath, "8", component,
            interactive: true, confirm: () => { prompted = true; return ConfirmResult.No; });

        uninstall.Should().Throw<DotnetInstallException>()
            .Which.ErrorCode.Should().Be(DotnetInstallErrorCode.OperationCancelled);
        prompted.Should().BeTrue();
        File.ReadAllText(environment.ManifestPath).Should().Be(before);
        manifest.GetInstallations(root).Should().ContainSingle();
    }

    [TestMethod]
    public void Execute_SharedTarget_AcceptRemovesOnlyRequestedSpec()
    {
        using var environment = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(environment.ManifestPath);
        var root = new DotnetInstallRoot(environment.InstallPath, InstallArchitecture.x64);
        AddInstallation(environment, manifest, root, InstallComponent.SDK, "8.0.100");
        AddSpec(manifest, root, InstallComponent.SDK, "8");
        AddSpec(manifest, root, InstallComponent.SDK, "8.0.100");

        UninstallWorkflow.Execute(environment.ManifestPath, environment.InstallPath, "8",
            InstallComponent.SDK, interactive: true, confirm: () => ConfirmResult.Yes);

        manifest.GetInstallations(root).Should().ContainSingle();
        manifest.GetInstallSpecs(root).Should().ContainSingle().Which.VersionOrChannel.Should().Be("8.0.100");
    }

    [TestMethod]
    public void Execute_Decline_DoesNotPersistPruningOrGlobalJsonRefresh()
    {
        using var environment = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(environment.ManifestPath);
        var root = new DotnetInstallRoot(environment.InstallPath, InstallArchitecture.x64);
        AddInstallation(environment, manifest, root, InstallComponent.SDK, "8.0.100");
        AddSpec(manifest, root, InstallComponent.SDK, "8");
        AddSpec(manifest, root, InstallComponent.SDK, "8.0.100");
        manifest.AddInstallSpec(root, new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "9",
            InstallSource = InstallSource.GlobalJson,
            GlobalJsonPath = Path.Combine(environment.TempRoot, "missing", "global.json")
        });
        var data = manifest.ReadManifestWithoutPruning();
        data.DotnetRoots.Add(new DotnetRootEntry { Path = Path.Combine(environment.TempRoot, "missing-root") });
        manifest.WriteManifest(data);
        var before = File.ReadAllText(environment.ManifestPath);

        Action uninstall = () => UninstallWorkflow.Execute(
            environment.ManifestPath, environment.InstallPath, "8", InstallComponent.SDK,
            interactive: true, confirm: () => ConfirmResult.No);

        uninstall.Should().Throw<DotnetInstallException>();
        File.ReadAllText(environment.ManifestPath).Should().Be(before);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Execute_UnexpectedRemoval_ConfirmsBeforeChangingState(bool accept)
    {
        using var environment = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(environment.ManifestPath);
        var root = new DotnetInstallRoot(environment.InstallPath, InstallArchitecture.x64);
        AddInstallation(environment, manifest, root, InstallComponent.SDK, "8.0.100");
        AddInstallation(environment, manifest, root, InstallComponent.Runtime, "9.0.0");
        AddSpec(manifest, root, InstallComponent.SDK, "8");
        var before = File.ReadAllText(environment.ManifestPath);
        var prompts = 0;

        Action uninstall = () => UninstallWorkflow.Execute(
            environment.ManifestPath, environment.InstallPath, "8", InstallComponent.SDK,
            interactive: true, confirm: () =>
            {
                prompts++;
                File.ReadAllText(environment.ManifestPath).Should().Be(before);
                manifest.GetInstallations(root).Should().HaveCount(2);
                return accept ? ConfirmResult.Yes : ConfirmResult.No;
            });

        if (accept)
        {
            uninstall();
            manifest.GetInstallations(root).Should().BeEmpty();
        }
        else
        {
            uninstall.Should().Throw<DotnetInstallException>();
            File.ReadAllText(environment.ManifestPath).Should().Be(before);
            manifest.GetInstallations(root).Should().HaveCount(2);
        }

        prompts.Should().Be(1);
    }

    [TestMethod]
    public void Execute_NonInteractive_DoesNotPromptAndPreservesGcBehavior()
    {
        using var environment = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(environment.ManifestPath);
        var root = new DotnetInstallRoot(environment.InstallPath, InstallArchitecture.x64);
        AddInstallation(environment, manifest, root, InstallComponent.SDK, "8.0.100");
        AddInstallation(environment, manifest, root, InstallComponent.SDK, "9.0.100");
        AddSpec(manifest, root, InstallComponent.SDK, "8");

        UninstallWorkflow.Execute(environment.ManifestPath, environment.InstallPath, "8",
            InstallComponent.SDK, interactive: false, confirm: () => throw new AssertFailedException("Unexpected prompt."));

        manifest.GetInstallations(root).Should().BeEmpty();
        Directory.GetDirectories(Path.Combine(environment.InstallPath, "sdk")).Should().BeEmpty();
    }

    [TestMethod]
    public void Execute_BothWarnings_RequiresOnlyOneConfirmation()
    {
        using var environment = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(environment.ManifestPath);
        var root = new DotnetInstallRoot(environment.InstallPath, InstallArchitecture.x64);
        AddInstallation(environment, manifest, root, InstallComponent.SDK, "8.0.100");
        AddInstallation(environment, manifest, root, InstallComponent.SDK, "9.0.100");
        AddSpec(manifest, root, InstallComponent.SDK, "8");
        AddSpec(manifest, root, InstallComponent.SDK, "8.0.100");
        var prompts = 0;

        UninstallWorkflow.Execute(environment.ManifestPath, environment.InstallPath, "8",
            InstallComponent.SDK, interactive: true, confirm: () => { prompts++; return ConfirmResult.Yes; });

        prompts.Should().Be(1);
        manifest.GetInstallations(root).Should().ContainSingle().Which.Version.Should().Be("8.0.100");
    }

    [TestMethod]
    public void Execute_ExpectedRemoval_DoesNotPrompt()
    {
        using var environment = new TestEnvironment();
        using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
        var manifest = new DotnetupSharedManifest(environment.ManifestPath);
        var root = new DotnetInstallRoot(environment.InstallPath, InstallArchitecture.x64);
        AddInstallation(environment, manifest, root, InstallComponent.SDK, "8.0.100");
        AddSpec(manifest, root, InstallComponent.SDK, "8");

        UninstallWorkflow.Execute(environment.ManifestPath, environment.InstallPath, "8",
            InstallComponent.SDK, interactive: true, confirm: () => throw new AssertFailedException("Unexpected prompt."));

        manifest.GetInstallations(root).Should().BeEmpty();
    }

    [TestMethod]
    [DataRow(new[] { "sdk", "uninstall", "8", "--manifest-path", "manifest.json", "--install-path", "dotnet", "--interactive", "false", "--non-interactive" })]
    [DataRow(new[] { "uninstall", "8", "--manifest-path", "manifest.json", "--install-path", "dotnet", "--interactive", "false", "--non-interactive" })]
    [DataRow(new[] { "runtime", "uninstall", "8", "--manifest-path", "manifest.json", "--install-path", "dotnet", "--interactive", "false", "--non-interactive" })]
    public void Parser_UninstallAcceptsSharedOptions(string[] args)
    {
        var result = Parser.Parse(args);

        result.Errors.Should().BeEmpty();
        result.GetValue(CommonOptions.ManifestPathOption).Should().Be("manifest.json");
        result.GetValue(CommonOptions.InstallPathOption).Should().Be("dotnet");
        result.GetValue(CommonOptions.InteractiveOption).Should().BeFalse();
        result.GetValue(CommonOptions.NonInteractiveOption).Should().BeTrue();
    }

    private static void AddSpec(
        DotnetupSharedManifest manifest, DotnetInstallRoot root, InstallComponent component, string channel)
    {
        manifest.AddInstallSpec(root, new InstallSpec
        {
            Component = component,
            VersionOrChannel = channel,
            InstallSource = InstallSource.Explicit
        });
    }

    private static void AddInstallation(
        TestEnvironment environment, DotnetupSharedManifest manifest, DotnetInstallRoot root,
        InstallComponent component, string version)
    {
        environment.StubComponentDirectories(root.Path, (component, version));
        var path = component == InstallComponent.SDK
            ? $"sdk/{version}"
            : $"shared/{component.GetFrameworkName()}/{version}";
        manifest.AddInstallation(root, new Installation
        {
            Component = component,
            Version = version,
            Subcomponents = [path]
        });
    }

    /// <summary>
    /// When no explicit path is provided and dotnet on PATH resolves to an admin install,
    /// the uninstall should fall back to the default hive — not the admin path.
    /// Regression test: previously, the configured path was used unconditionally,
    /// causing uninstall to target "C:\Program Files\dotnet" when the user meant their user install.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_AdminInstall_FallsBackToDefault()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: CreateConfig(AdminPath, isDotnetupHive: false));

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath, "installs on PATH that dotnetup does not own should not be used; default hive should be used instead");
    }

    /// <summary>
    /// When dotnet on PATH resolves to the dotnetup-managed hive, the uninstall should use that path.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_DotnetupHive_UsesConfiguredPath()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: CreateConfig(DefaultUserPath, isDotnetupHive: true));

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath, "the dotnetup hive on PATH should be used");
    }

    /// <summary>
    /// A dotnet that lives in a user-writable location but is not a dotnetup hive (e.g. a
    /// hand-extracted C:\dotnet that happens to win on PATH) must not be treated as dotnetup's;
    /// uninstall should fall back to the default hive rather than target the unmanaged install.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_UnmanagedUserInstall_FallsBackToDefault()
    {
        string looseUserPath = "/home/user/custom-dotnet";
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: CreateConfig(looseUserPath, isDotnetupHive: false));

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath, "a non-hive install on PATH should not be uninstalled; default hive should be used");
    }

    /// <summary>
    /// Explicit --install-path always takes priority.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_ExplicitPath_TakesPrecedence()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: CreateConfig(AdminPath, isDotnetupHive: false));

        var result = UninstallWorkflow.ResolveInstallPath(ExplicitPath, mock);

        result.Should().Be(ExplicitPath, "explicit path should always win");
    }

    /// <summary>
    /// When no dotnet is on PATH and no explicit path is given, the default user path is used.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_NoConfiguredInstall_UsesDefault()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: null);

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath);
    }

    private static DotnetInstallRootConfiguration CreateConfig(string path, bool isDotnetupHive)
    {
        var installRoot = new DotnetInstallRoot(path, InstallerUtilities.GetDefaultInstallArchitecture());
        return new DotnetInstallRootConfiguration(installRoot, isDotnetupHive);
    }
}

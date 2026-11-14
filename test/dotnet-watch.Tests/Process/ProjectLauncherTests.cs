// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class ProjectLauncherTests : DotNetWatchTestBase
{
    private const string EnvironmentHook = "Environment.dll";
    private const string ProfileHook = "Profile.dll";
    private const string NoLaunchProfile = "--no-launch-profile";

    /// <summary>
    /// The Hot Reload agent's startup hook is added in front of the hooks the application would receive without dotnet watch:
    /// the value defined by the launch profile `dotnet run` selects, if any, otherwise the value inherited from dotnet watch's environment.
    /// </summary>
    /// <param name="environmentHook">Value of DOTNET_STARTUP_HOOKS in dotnet watch's environment, or null if not set.</param>
    /// <param name="profileHook">Value of DOTNET_STARTUP_HOOKS in the environmentVariables of profile "Hooks", or null if the profile doesn't define it.</param>
    /// <param name="launchProfile">Requested profile name, null for the default profile, or <see cref="NoLaunchProfile"/>.</param>
    /// <param name="nonStringValueInOtherProfile">Add a non-string environment variable value to profile "Other".</param>
    /// <param name="expectedExistingHook">The hook expected after the agent's hook, or null if the agent's hook is expected to be the only one.</param>
    [TestMethod]
    [DataRow(EnvironmentHook, null, null, false, EnvironmentHook, DisplayName = "environment")]
    [DataRow(null, ProfileHook, null, false, ProfileHook, DisplayName = "default launch profile")]
    [DataRow(null, ProfileHook, "Hooks", false, ProfileHook, DisplayName = "named launch profile")]
    [DataRow(EnvironmentHook, ProfileHook, null, false, ProfileHook, DisplayName = "launch profile overrides environment")]
    [DataRow(null, null, null, false, null, DisplayName = "neither")]
    [DataRow(EnvironmentHook, ProfileHook, NoLaunchProfile, false, EnvironmentHook, DisplayName = "no launch profile")]
    [DataRow(EnvironmentHook, "", null, false, null, DisplayName = "launch profile clears environment")]
    [DataRow(EnvironmentHook, ProfileHook, "hooks", false, ProfileHook, DisplayName = "launch profile name is case-insensitive")]
    [DataRow(EnvironmentHook, ProfileHook, "Missing", false, EnvironmentHook, DisplayName = "missing launch profile")]
    [DataRow(null, ProfileHook, "Hooks", true, ProfileHook, DisplayName = "non-string value in another launch profile")]
    public async Task StartupHooks_ComposedWithExistingValue(string? environmentHook, string? profileHook, string? launchProfile, bool nonStringValueInOtherProfile, string? expectedExistingHook)
    {
        var testAsset = CopyTestAsset("WatchNoDepsApp", [environmentHook ?? "null", profileHook ?? "null", launchProfile ?? "null", nonStringValueInOtherProfile]);

        // The profile values are MSBuild expressions to check that they are expanded the same way `dotnet run` expands them.
        var hooksVariables = profileHook is null ? "" : $$""", "environmentVariables": { "DOTNET_STARTUP_HOOKS": {{GetProfileValue(profileHook)}} }""";
        var hooksProfile = $$""" "Hooks": { "commandName": "Project"{{hooksVariables}} }""";

        // A different supported profile with a different hook, so that selecting the wrong profile is observable:
        var otherVariables = $$""" "DOTNET_STARTUP_HOOKS": {{GetProfileValue("Other.dll")}}""" + (nonStringValueInOtherProfile ? """, "PORT": 1234""" : "");
        var otherProfile = $$""" "Other": { "commandName": "Project", "environmentVariables": { {{otherVariables}} } }""";

        // The default profile is the first one:
        var profiles = launchProfile is null ? $"{hooksProfile}, {otherProfile}" : $"{otherProfile}, {hooksProfile}";

        var (startupHooks, agentHook) = await GetStartupHooksArgumentAsync(
            testAsset,
            $$"""{ "profiles": { {{profiles}} } }""",
            environmentHook,
            launchProfile switch
            {
                null => new Optional<string?>(null),
                NoLaunchProfile => Optional<string?>.NoValue,
                _ => new Optional<string?>(launchProfile),
            });

        Assert.AreEqual(GetExpectedStartupHooks(testAsset, agentHook, expectedExistingHook), startupHooks);
    }

    /// <summary>
    /// `dotnet run` sets the profile's environment variables on the process, whose variable names are case-insensitive on Windows.
    /// </summary>
    [TestMethod]
    public async Task StartupHooks_ProfileVariableNameCase()
    {
        var testAsset = CopyTestAsset("WatchNoDepsApp");

        var (startupHooks, agentHook) = await GetStartupHooksArgumentAsync(
            testAsset,
            $$"""{ "profiles": { "Hooks": { "commandName": "Project", "environmentVariables": { "dotnet_startup_hooks": {{GetProfileValue(ProfileHook)}} } } } }""",
            environmentHook: EnvironmentHook,
            launchProfileName: new Optional<string?>(null));

        Assert.AreEqual(GetExpectedStartupHooks(testAsset, agentHook, OperatingSystem.IsWindows() ? ProfileHook : EnvironmentHook), startupHooks);
    }

    /// <summary>
    /// A null value in the selected profile can't be parsed. Watch keeps running and uses the inherited value.
    /// </summary>
    [TestMethod]
    public async Task StartupHooks_NullValueInSelectedProfile()
    {
        var testAsset = CopyTestAsset("WatchNoDepsApp");

        var (startupHooks, agentHook) = await GetStartupHooksArgumentAsync(
            testAsset,
            $$"""{ "profiles": { "Hooks": { "commandName": "Project", "environmentVariables": { "DOTNET_STARTUP_HOOKS": {{GetProfileValue(ProfileHook)}}, "PORT": null } } } }""",
            environmentHook: EnvironmentHook,
            launchProfileName: new Optional<string?>(null));

        Assert.AreEqual(GetExpectedStartupHooks(testAsset, agentHook, EnvironmentHook), startupHooks);
    }

    private static string GetProfileValue(string hook)
        => hook is "" ? "\"\"" : $"\"$(MSBuildProjectDirectory){JsonEncodedText.Encode(Path.DirectorySeparatorChar + hook)}\"";

    private static string GetExpectedStartupHooks(TestAsset testAsset, string agentHook, string? existingHook)
        => existingHook is null ? agentHook : agentHook + Path.PathSeparator + Path.Combine(testAsset.Path, existingHook);

    /// <summary>
    /// Launches the project with a process runner that captures the `dotnet run` command line instead of starting the process,
    /// and returns the value of the DOTNET_STARTUP_HOOKS variable passed to it, along with the path of the Hot Reload agent's hook.
    /// </summary>
    private async Task<(string startupHooks, string agentHook)> GetStartupHooksArgumentAsync(TestAsset testAsset, string launchSettings, string? environmentHook, Optional<string?> launchProfileName)
    {
        var projectDirectory = testAsset.Path;
        var projectPath = Path.Combine(projectDirectory, "WatchNoDepsApp.csproj");

        Directory.CreateDirectory(Path.Combine(projectDirectory, "Properties"));
        File.WriteAllText(Path.Combine(projectDirectory, "Properties", "launchSettings.json"), launchSettings);

        var environmentOptions = TestOptions.GetEnvironmentOptions(projectDirectory, testAsset) with
        {
            DotNetStartupHooks = environmentHook is null ? null : Path.Combine(projectDirectory, environmentHook),
        };

        var projectRepresentation = new ProjectRepresentation(projectPath, entryPointFilePath: null);
        var graphFactory = new ProjectGraphFactory([projectRepresentation], buildProperties: [], NullLogger.Instance, TestOptions.GlobalOptions, environmentOptions);
        var projectGraph = graphFactory.TryLoadProjectGraph(projectGraphRequired: true, virtualProjectTargetFramework: null, CancellationToken.None);
        Assert.IsNotNull(projectGraph);

        var projectNode = projectGraph.TryGetProjectNode(projectPath, targetFramework: null);
        Assert.IsNotNull(projectNode);

        IReadOnlyList<string>? launchArguments = null;
        var processRunner = new TestProcessRunner()
        {
            RunImpl = (processSpec, _, _) =>
            {
                launchArguments = processSpec.Arguments;

                // report that the process failed to start:
                return int.MinValue;
            }
        };

        var logger = new TestLogger(Logger);
        var processOutputReporter = new TestProcessOutputReporter();
        using var context = new DotNetWatchContext()
        {
            ProcessOutputReporter = processOutputReporter,
            Logger = logger,
            BuildLogger = logger,
            LoggerFactory = new TestLoggerFactory(Logger),
            ProcessRunner = processRunner,
            Options = TestOptions.GlobalOptions,
            MainProjectOptions = null,
            RootProjects = [projectRepresentation],
            BuildArguments = [],
            EnvironmentOptions = environmentOptions,
            BrowserLauncher = new BrowserLauncher(logger, processOutputReporter, environmentOptions),
            BrowserRefreshServerFactory = new BrowserRefreshServerFactory(),
        };

        var launcher = new ProjectLauncher(context, projectGraph, new RunningProjectsManager(processRunner, logger), iteration: 0);

        var projectOptions = new ProjectOptions()
        {
            IsMainProject = true,
            Representation = projectRepresentation,
            WorkingDirectory = projectDirectory,
            Command = "run",
            CommandArguments = [],
            LaunchEnvironmentVariables = [],
            LaunchProfileName = launchProfileName,
        };

        var runningProject = await launcher.TryLaunchProcessAsync(projectOptions, onOutput: null, onExit: null, new RestartOperation(_ => default), CancellationToken.None);

        // the process has not been started:
        Assert.IsNull(runningProject);
        Assert.IsNotNull(launchArguments);

        const string prefix = "DOTNET_STARTUP_HOOKS=";
        var startupHooksArguments = launchArguments
            .Select((argument, index) => (argument, index))
            .Where(a => a.argument.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        Assert.HasCount(1, startupHooksArguments);
        Assert.AreEqual("-e", launchArguments[startupHooksArguments[0].index - 1]);

        return (startupHooksArguments[0].argument[prefix.Length..], HotReloadAppModel.GetStartupHookPath(projectNode));
    }

    /// <summary>
    /// The launch profile sets DOTNET_STARTUP_HOOKS to a hook that prints the name of the entry assembly of the process it runs in.
    /// </summary>
    [TestMethod]
    public async Task StartupHookFromLaunchProfile_RunsInApplication()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithStartupHook")
            .WithSource();

        App.Start(testAsset, [], "App");

        await AssertStartupHookRanInApplicationAsync();
    }

    /// <summary>
    /// DOTNET_STARTUP_HOOKS is set in the environment of dotnet watch. The hook also runs in dotnet watch and in the processes it starts,
    /// where it prints their own entry assembly names.
    /// </summary>
    [TestMethod]
    public async Task StartupHookFromEnvironment_RunsInApplication()
    {
        var testAsset = TestAssets.CopyTestAsset("WatchAppWithStartupHook")
            .WithSource();

        // The hook must exist before dotnet watch starts, since it is loaded into dotnet watch as well:
        var buildResult = new DotnetCommand(Logger, ["build", Path.Combine(testAsset.Path, "Hook", "Hook.csproj")])
        {
            WorkingDirectory = testAsset.Path,
        }.Execute();

        Assert.AreEqual(0, buildResult.ExitCode);

        // Load the hook from a copy, so that the build dotnet watch runs never writes to a file loaded in a running process.
        var hookOutputDirectory = Path.Combine(testAsset.Path, "Hook", "bin", "Debug", ToolsetInfo.CurrentTargetFramework);
        var hookDirectory = Path.Combine(testAsset.Path, "StartupHook");
        Directory.CreateDirectory(hookDirectory);
        foreach (var fileName in new[] { "Hook.dll", "Hook.deps.json" })
        {
            File.Copy(Path.Combine(hookOutputDirectory, fileName), Path.Combine(hookDirectory, fileName));
        }

        App.EnvironmentVariables.Add("DOTNET_STARTUP_HOOKS", Path.Combine(hookDirectory, "Hook.dll"));

        // Processes started by dotnet watch inherit the hook. Don't start a compiler server with it, which other tests could reuse:
        App.EnvironmentVariables.Add("UseSharedCompilation", "false");

        // The launch profile of the app also sets the variable, which would override the inherited value:
        App.Start(testAsset, ["--no-launch-profile"], "App");

        await AssertStartupHookRanInApplicationAsync();
    }

    private async Task AssertStartupHookRanInApplicationAsync()
    {
        // Wait for Main to start, or for the application to exit (e.g. when a startup hook fails to load).
        await App.WaitUntilOutputContains(new Regex("App started|" + Regex.Escape(MessageDescriptor.WaitingForFileChangeBeforeRestarting.Format!)));

        // The hook runs before Main:
        App.AssertOutputContains("App started");
        App.AssertOutputContains("Startup hook initialized in 'App'");
    }
}

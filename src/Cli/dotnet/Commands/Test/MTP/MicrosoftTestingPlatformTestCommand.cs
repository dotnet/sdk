// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.Runtime.CompilerServices;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal partial class MicrosoftTestingPlatformTestCommand
{
    public int Run(ParseResult parseResult, bool isHelp)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        BuildOptions buildOptions = MSBuildUtility.GetBuildOptions(parseResult);
        ValidationUtility.ValidateMutuallyExclusiveOptions(parseResult, buildOptions.PathOptions);

        // --list-devices and --list-tests describe incompatible behaviors: the former lists
        // devices and exits without building, the latter discovers tests in built assemblies.
        if (buildOptions.ListDevices && parseResult.HasOption(definition.ListTestsOption))
        {
            throw new GracefulException(CliCommandStrings.CmdListDevicesAndListTestsMutuallyExclusive);
        }

        // --list-devices and --device require a project to evaluate; --test-modules bypasses
        // project evaluation entirely, so the combination is meaningless.
        if (buildOptions.PathOptions.TestModules is not null
            && (buildOptions.ListDevices || !string.IsNullOrWhiteSpace(buildOptions.Device)))
        {
            throw new GracefulException(CliCommandStrings.CmdDeviceOptionsRequireProject);
        }

        // --list-devices: list available devices for the project and exit early.
        // Never builds, deploys, or runs tests.
        if (buildOptions.ListDevices)
        {
            return HandleListDevices(buildOptions);
        }

        // When --device is specified, force single target framework selection because
        // a device is platform-specific and we need to know which TFM was intended.
        if (!string.IsNullOrWhiteSpace(buildOptions.Device))
        {
            buildOptions = HandleDeviceWithTargetFrameworkSelection(buildOptions);
        }

        ITestHandler testHandler = buildOptions.PathOptions.TestModules is { } testModules
            ? new TestModulesFilterHandler(testModules, parseResult)
            : RuntimeFeature.IsDynamicCodeSupported ? new MSBuildHandler(buildOptions)
                : throw new PlatformNotSupportedException("Dynamic code is not supported on this platform.");

        if (!testHandler.Initialize())
        {
            return ExitCode.GenericFailure;
        }

        int degreeOfParallelism = GetDegreeOfParallelism(parseResult);

        var testOptions = new TestOptions(
            IsHelp: isHelp,
            IsDiscovery: parseResult.HasOption(definition.ListTestsOption),
            EnvironmentVariables: parseResult.GetValue(definition.EnvOption) ?? ImmutableDictionary<string, string>.Empty);

        var output = InitializeOutput(degreeOfParallelism, parseResult, testOptions);
        using var ctrlC = new CtrlCCancellationManager(output.StartCancelling);
        int? exitCode = null;
        try
        {
            var actionQueue = new TestApplicationActionQueue(degreeOfParallelism, buildOptions, testOptions, output, OnHelpRequested, ctrlC);
            exitCode = testHandler.RunTestApplications(actionQueue);

            // If all test apps exited with 0 exit code, but we detected that handshake didn't happen correctly, map that to generic failure.
            if (exitCode == ExitCode.Success && output.HasHandshakeFailure)
            {
                exitCode = ExitCode.GenericFailure;
            }

            if (exitCode == ExitCode.Success &&
                parseResult.HasOption(definition.MinimumExpectedTestsOption) &&
                parseResult.GetValue(definition.MinimumExpectedTestsOption) is { } minimumExpectedTests &&
                output.TotalTests < minimumExpectedTests)
            {
                exitCode = ExitCode.MinimumExpectedTestsPolicyViolation;
            }

            return exitCode.Value;
        }
        finally
        {
            output.TestExecutionCompleted(DateTimeOffset.Now, exitCode);
        }
    }

    private static TerminalTestReporter InitializeOutput(int degreeOfParallelism, ParseResult parseResult, TestOptions testOptions)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        var console = new SystemConsole();
        var showPassedTests = parseResult.GetValue(definition.OutputOption) == OutputOptions.Detailed;
        var noProgress = parseResult.HasOption(definition.NoProgressOption);
        var noAnsi = parseResult.HasOption(definition.NoAnsiOption);

        // TODO: Replace this with proper CI detection that we already have in telemetry. https://github.com/microsoft/testfx/issues/5533#issuecomment-2838893327
        bool inCI = string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        AnsiMode ansiMode = AnsiMode.AnsiIfPossible;
        // In LLM environments, prefer simple text output so that LLM can parse it easily.
        // Note that NoAnsi also implies no progress.
        if (noAnsi || new LLMEnvironmentDetectorForTelemetry().IsLLMEnvironment())
        {
            // User explicitly specified --no-ansi.
            // We should respect that.
            ansiMode = AnsiMode.NoAnsi;
        }
        else if (inCI)
        {
            ansiMode = AnsiMode.SimpleAnsi;
        }

        var output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
        {
            ShowPassedTests = showPassedTests,
            ShowProgress = !noProgress,
            ShowActiveTests = !noProgress && ansiMode == AnsiMode.AnsiIfPossible,
            AnsiMode = ansiMode,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
            MinimumExpectedTests = parseResult.GetValue(definition.MinimumExpectedTestsOption),
        });

        // Ctrl+C handling is wired in Run() through CtrlCCancellationManager so that
        // a second press can force-kill running test app child processes and exit with
        // ExitCode.TestSessionAborted (see issue https://github.com/dotnet/sdk/issues/50732).

        // This is ugly, and we need to replace it by passing out some info from testing platform to inform us that some process level retry plugin is active.
        var isRetry = parseResult.GetArguments().Contains("--retry-failed-tests");

        output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, testOptions.IsDiscovery, testOptions.IsHelp, isRetry);
        return output;
    }

    private static int GetDegreeOfParallelism(ParseResult parseResult)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        var degreeOfParallelism = parseResult.GetValue(definition.MaxParallelTestModulesOption);
        if (degreeOfParallelism <= 0)
            degreeOfParallelism = Environment.ProcessorCount;
        return degreeOfParallelism;
    }

    /// <summary>
    /// When --device is specified, we need to ensure a single target framework is selected
    /// because a device is platform-specific. If -f/--framework wasn't provided, this method
    /// evaluates the project to get TargetFrameworks and prompts for selection.
    /// The selected device is also added to MSBuild args so the build sees it.
    /// Solutions are rejected because each project may have its own device list, so
    /// applying a single --device value across a solution is ambiguous.
    /// </summary>
    private static BuildOptions HandleDeviceWithTargetFrameworkSelection(BuildOptions buildOptions)
    {
        var msbuildArgs = SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(buildOptions.MSBuildArgs);

        var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs);

        // Device selection requires a single project (each project may have its own
        // device list). Reject solutions up front, regardless of whether -f/--framework
        // was provided, so `--device` + `-f` + `--solution` fails the same as `--device`
        // + `--solution` (which mirrors `--list-devices` + `--solution`).
        if (!ValidationUtility.ValidateBuildPathOptions(buildOptions.PathOptions, out var projectPath, out bool isSolution))
        {
            throw new GracefulException(CliCommandStrings.CmdTestNoTestProjectsFound);
        }

        if (isSolution)
        {
            throw new GracefulException(CliCommandStrings.TestCommandUseProject);
        }

        // Check if TargetFramework is already specified via -f/--framework or -p:TargetFramework=
        if (!globalProperties.ContainsKey(ProjectProperties.TargetFramework))
        {
            // Evaluate the project to get TargetFrameworks
            using var collection = new ProjectCollection(globalProperties);
            var projectInstance = ProjectInstance.FromFile(projectPath, new ProjectOptions
            {
                GlobalProperties = globalProperties,
                ProjectCollection = collection,
            });

            var targetFramework = projectInstance.GetPropertyValue(ProjectProperties.TargetFramework);
            var targetFrameworks = projectInstance.GetPropertyValue(ProjectProperties.TargetFrameworks);

            // Only prompt if multi-targeted (no single TargetFramework set)
            if (string.IsNullOrEmpty(targetFramework) && !string.IsNullOrEmpty(targetFrameworks))
            {
                var frameworks = targetFrameworks
                    .Split(CliConstants.SemiColon, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToArray();

                bool isInteractive = !Console.IsOutputRedirected && !new Telemetry.CIEnvironmentDetectorForTelemetry().IsCIEnvironment();
                if (!RunCommandSelector.TrySelectTargetFramework(frameworks, isInteractive, "dotnet test", out string? selectedFramework))
                {
                    // Error already written to stderr by TrySelectTargetFramework
                    throw new GracefulException(
                        string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
                }

                if (selectedFramework is not null)
                {
                    buildOptions = buildOptions with
                    {
                        MSBuildArgs = [.. buildOptions.MSBuildArgs, $"-p:{ProjectProperties.TargetFramework}={selectedFramework}"]
                    };
                }
            }
        }

        // Add Device to MSBuild args so the build and evaluation see it
        return buildOptions with
        {
            MSBuildArgs = [.. buildOptions.MSBuildArgs, $"-p:Device={buildOptions.Device}"]
        };
    }

    /// <summary>
    /// Handles `dotnet test --list-devices`. Resolves the project, prompts for
    /// target framework if multi-targeted, lists devices via
    /// <see cref="RunCommandSelector.TrySelectDevice"/>, and exits without
    /// building, deploying, or running tests.
    /// </summary>
    private static int HandleListDevices(BuildOptions buildOptions)
    {
        if (!ValidationUtility.ValidateBuildPathOptions(buildOptions.PathOptions, out var projectPath, out bool isSolution))
        {
            throw new GracefulException(CliCommandStrings.CmdTestNoTestProjectsFound);
        }

        if (isSolution)
        {
            // Listing devices across a solution is ambiguous: each project may have its own
            // set of devices. Require the user to pick a specific project via --project.
            throw new GracefulException(CliCommandStrings.TestCommandUseProject);
        }

        bool isInteractive = !Console.IsOutputRedirected && !new CIEnvironmentDetectorForTelemetry().IsCIEnvironment();

        var standardArgs = SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(buildOptions.MSBuildArgs);

        // Mirror the `dotnet run --list-devices` flow: a single RunCommandSelector
        // handles both target framework selection and device listing, with
        // InvalidateGlobalProperties between steps so the device list is computed
        // for the selected framework.
        using var selector = new RunCommandSelector(
            projectPath,
            isInteractive,
            standardArgs,
            ImmutableDictionary<string, string>.Empty,
            commandName: "dotnet test");

        // Step 1: Prompt for TargetFramework if the project is multi-targeted and -f wasn't provided.
        if (!selector.TrySelectTargetFramework(out string? selectedFramework))
        {
            // Mirror `dotnet run --list-devices` (RunCommand.cs:164): the guidance has
            // already been printed; `--list-devices` itself is not a build, so a missing
            // framework selection is not a failure to exit non-zero on.
            return ExitCode.Success;
        }

        if (selectedFramework is not null)
        {
            selector.InvalidateGlobalProperties(new Dictionary<string, string>
            {
                { ProjectProperties.TargetFramework, selectedFramework }
            });
        }

        // Step 2: List devices. This calls ComputeAvailableDevices if the target exists;
        // otherwise it silently no-ops (matches `dotnet run --list-devices`).
        if (!selector.TrySelectDevice(
            listDevices: true,
            noRestore: buildOptions.HasNoRestore || buildOptions.HasNoBuild,
            out _,
            out _,
            out _))
        {
            return ExitCode.GenericFailure;
        }

        return ExitCode.Success;
    }
}

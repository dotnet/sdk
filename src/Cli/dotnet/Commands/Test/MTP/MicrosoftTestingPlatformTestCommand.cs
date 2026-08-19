// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.Globalization;
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
    private const string MinimumExpectedTestsOptionName = "--minimum-expected-tests";

    public int Run(ParseResult parseResult, bool isHelp)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;
        string invocationWorkingDirectory = Directory.GetCurrentDirectory();

        BuildOptions buildOptions = MSBuildUtility.GetBuildOptions(parseResult);
        (buildOptions, bool forwardedCollectTestMap, bool forwardedAffectedTests) =
            NormalizeForwardedAffectedTestsOptions(buildOptions);
        bool forwardedMinimumExpectedTests = HasForwardedOption(
            buildOptions.TestApplicationArguments,
            MinimumExpectedTestsOptionName);

        bool collectTestMap = parseResult.HasOption(definition.CollectTestMapOption) || forwardedCollectTestMap;
        bool affectedTests = parseResult.HasOption(definition.AffectedTestsOption) || forwardedAffectedTests;
        ValidateAffectedTestsOptions(
            definition,
            parseResult,
            collectTestMap,
            affectedTests,
            forwardedMinimumExpectedTests);

        ValidationUtility.ValidateMutuallyExclusiveOptions(parseResult, buildOptions.PathOptions);

        // --list-devices and --list-tests describe incompatible behaviors: the former lists
        // devices and exits without building, the latter discovers tests in built assemblies.
        if (buildOptions.ListDevices && parseResult.HasOption(definition.ListTestsOption))
        {
            throw new GracefulException(CliCommandStrings.CmdListDevicesAndListTestsMutuallyExclusive);
        }

        if (buildOptions.ListDevices && (collectTestMap || affectedTests))
        {
            throw new GracefulException(CliCommandStrings.CmdListDevicesAndAffectedTestsMutuallyExclusive);
        }

        // --list-devices and --device require a project to evaluate; --test-modules bypasses
        // project evaluation entirely, so the combination is meaningless.
        if (buildOptions.PathOptions.TestModules is not null
            && (buildOptions.ListDevices || !string.IsNullOrWhiteSpace(buildOptions.Device)))
        {
            throw new GracefulException(CliCommandStrings.CmdDeviceOptionsRequireProject);
        }

        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. buildOptions.MSBuildArgs], "dotnet-test");
        ITestHandler testHandler;
        MSBuildSession? buildSession = null;
        try
        {
            // --list-devices: list available devices for the project and exit early.
            // Never builds, deploys, or runs tests.
            if (buildOptions.ListDevices)
            {
                using var listDevicesSession = CreateBuildSession(buildOptions, logger);
                int listDevicesExitCode = HandleListDevices(buildOptions, listDevicesSession);
                if (listDevicesExitCode == ExitCode.Success)
                {
                    listDevicesSession.Complete();
                }

                return listDevicesExitCode;
            }

            // When --device is specified, force single target framework selection because
            // a device is platform-specific and we need to know which TFM was intended.
            if (!string.IsNullOrWhiteSpace(buildOptions.Device))
            {
                buildOptions = HandleDeviceWithTargetFrameworkSelection(buildOptions, logger);
            }

            // Every MSBuild target the test command invokes itself - device selection, deployment and
            // ComputeRunArguments for every test module - runs inside this single build session, so the
            // binary log behind -bl holds one well formed build instead of one per target invocation.
            buildSession = CreateBuildSession(buildOptions, logger);

            testHandler = buildOptions.PathOptions.TestModules is { } testModules
                ? new TestModulesFilterHandler(testModules, parseResult)
                : RuntimeFeature.IsDynamicCodeSupported ? new MSBuildHandler(buildOptions, buildSession)
                    : throw new PlatformNotSupportedException("Dynamic code is not supported on this platform.");

            if (!testHandler.Initialize())
            {
                return ExitCode.GenericFailure;
            }

            (bool responseFileCollectTestMap, bool responseFileAffectedTests, bool responseFileMinimumExpectedTests) =
                DetectAffectedTestsOptionsInForwardedResponseFiles(
                    buildOptions.TestApplicationArguments,
                    testHandler.GetTestApplicationWorkingDirectories(),
                    invocationWorkingDirectory);
            collectTestMap |= responseFileCollectTestMap;
            affectedTests |= responseFileAffectedTests;
            forwardedCollectTestMap |= responseFileCollectTestMap;
            forwardedAffectedTests |= responseFileAffectedTests;
            forwardedMinimumExpectedTests |= responseFileMinimumExpectedTests;

            // Ends the session on the success path, so a failure MSBuild only reports from EndBuild -
            // a binary logger failing to write, for example - is surfaced rather than swallowed.
            buildSession.Complete();
        }
        finally
        {
            // Ends the build session (and so writes its build-finished event) before the binary
            // logger behind it is shut down. A no-op once Complete() has run.
            buildSession?.Dispose();
            logger?.ReallyShutdown();
        }

        ValidateAffectedTestsOptions(
            definition,
            parseResult,
            collectTestMap,
            affectedTests,
            forwardedMinimumExpectedTests);

        int degreeOfParallelism = GetDegreeOfParallelism(parseResult, collectTestMap);

        var testOptions = new TestOptions(
            IsHelp: isHelp,
            IsDiscovery: parseResult.HasOption(definition.ListTestsOption),
            ListTestsFormat: GetListTestsFormat(parseResult, definition))
        {
            CollectTestMap = collectTestMap,
            AffectedTests = affectedTests,
            CollectTestMapForwarded = forwardedCollectTestMap,
            AffectedTestsForwarded = forwardedAffectedTests,
        };

        var output = InitializeOutput(degreeOfParallelism, parseResult, testOptions);
        var resultsDirectoryResolver = TestResultsDirectoryResolver.Create(
            buildOptions.PathOptions,
            testHandler.EnumerateTestModules(),
            Directory.GetCurrentDirectory());

        using var testRunPolicy = new TestRunPolicy(
            testOptions.IsDiscovery || testOptions.IsHelp
                ? null
                : parseResult.GetValue(definition.MaximumFailedTestsOption),
            testOptions.IsDiscovery || testOptions.IsHelp
                ? null
                : parseResult.GetValue(definition.TimeoutOption),
            onCancellation: _ => output.MarkCancelled());

        using var ctrlC = new CtrlCCancellationManager(output.StartCancelling);
        using var queueCancellation = CancellationTokenSource.CreateLinkedTokenSource(ctrlC.Token, testRunPolicy.Token);
        var artifactPostProcessingManager = new ArtifactPostProcessingManager();
        int? exitCode = null;
        try
        {
            var actionQueue = new TestApplicationActionQueue(
                degreeOfParallelism,
                buildOptions,
                testOptions,
                resultsDirectoryResolver,
                output,
                OnHelpRequested,
                ctrlC,
                artifactPostProcessingManager,
                testRunPolicy,
                queueCancellation.Token);
            exitCode = testHandler.RunTestApplications(actionQueue);
            TestRunCancellationReason cancellationReason = testRunPolicy.Complete();

            if (cancellationReason == TestRunCancellationReason.MaximumFailedTests)
            {
                exitCode = ExitCode.TestExecutionStoppedForMaxFailedTests;
            }
            else if (cancellationReason == TestRunCancellationReason.Timeout)
            {
                exitCode = ExitCode.TestSessionAborted;
            }

            if (ShouldPostProcessArtifacts(
                testOptions,
                parseResult.GetValue(definition.NoArtifactPostProcessingOption),
                ctrlC.Token.IsCancellationRequested,
                cancellationReason))
            {
                artifactPostProcessingManager.ExecuteAsync(buildOptions, output, ctrlC).GetAwaiter().GetResult();
            }

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
            else if (exitCode == ExitCode.Success &&
                !isHelp &&
                !parseResult.HasOption(definition.MinimumExpectedTestsOption) &&
                ShouldFailForNoExecutedTests(testOptions.IsAffectedTestsMode, output.TotalTests, output.SkippedTests))
            {
                // Whole-run "zero tests ran" verdict. Individual modules that matched no tests return exit
                // code 8, but TestApplicationActionQueue normalizes that to success so a single empty module
                // does not fail the whole run (e.g. with --test-modules or a global --filter). The aggregate
                // zero-tests case is decided here so it surfaces once at the run level instead of once per
                // module. A stricter per-module minimum via -- --minimum-expected-tests N still fails that
                // module with exit code 9. See https://github.com/microsoft/testfx/issues/7457.
                exitCode = ExitCode.ZeroTests;
            }

            return exitCode.Value;
        }
        finally
        {
            output.TestExecutionCompleted(DateTimeOffset.Now, exitCode);
        }
    }

    /// <summary>
    /// Decides whether the artifacts of a finished run should be consolidated.
    /// </summary>
    /// <remarks>
    /// Help and discovery produce no artifacts to merge, and <c>--no-artifact-post-processing</c> is
    /// the explicit opt-out. The two cancellation cases are the interesting ones: a run stopped by
    /// Ctrl+C, <c>--maximum-failed-tests</c> or <c>--timeout</c> produced the artifacts of a
    /// truncated run — modules that never started contributed nothing, and modules killed mid-flight
    /// wrote whatever they had. Merging those into a single report would hide the truncation behind
    /// one authoritative-looking artifact, so the per-module artifacts are left as they are.
    /// </remarks>
    internal static bool ShouldPostProcessArtifacts(
        TestOptions testOptions,
        bool noArtifactPostProcessingRequested,
        bool cancellationRequested,
        TestRunCancellationReason cancellationReason)
        => !testOptions.IsHelp
            && !testOptions.IsDiscovery
            && !noArtifactPostProcessingRequested
            && !cancellationRequested
            && cancellationReason == TestRunCancellationReason.None;

    internal static (BuildOptions BuildOptions, bool CollectTestMap, bool AffectedTests) NormalizeForwardedAffectedTestsOptions(
        BuildOptions buildOptions)
    {
        bool collectTestMap = false;
        bool affectedTests = false;
        ImmutableArray<string>.Builder remainingArguments = ImmutableArray.CreateBuilder<string>();
        foreach (string argument in buildOptions.TestApplicationArguments)
        {
            if (IsAffectedTestsOption(argument, TestCommandDefinition.MicrosoftTestingPlatform.CollectTestMapOptionName))
            {
                collectTestMap = true;
                remainingArguments.Add(argument);
            }
            else if (IsAffectedTestsOption(argument, TestCommandDefinition.MicrosoftTestingPlatform.AffectedTestsOptionName))
            {
                affectedTests = true;
                remainingArguments.Add(argument);
            }
            else
            {
                remainingArguments.Add(argument);
            }
        }

        return (
            buildOptions with { TestApplicationArguments = remainingArguments.ToImmutable() },
            collectTestMap,
            affectedTests);
    }

    private static void ValidateAffectedTestsOptions(
        TestCommandDefinition.MicrosoftTestingPlatform definition,
        ParseResult parseResult,
        bool collectTestMap,
        bool affectedTests,
        bool forwardedMinimumExpectedTests)
    {
        if (!definition.AffectedTestsEnabled && (collectTestMap || affectedTests))
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.CmdAffectedTestsFeatureDisabled,
                    TestCommandDefinition.MicrosoftTestingPlatform.EnableAffectedTestsEnvironmentVariable));
        }

        if (collectTestMap && affectedTests)
        {
            throw new GracefulException(CliCommandStrings.CmdAffectedTestsOptionsMutuallyExclusive);
        }

        if (collectTestMap && parseResult.HasOption(definition.MaxParallelTestModulesOption))
        {
            throw new GracefulException(CliCommandStrings.CmdCollectTestMapCannotRunModulesInParallel);
        }

        if (collectTestMap &&
            (parseResult.HasOption(definition.MinimumExpectedTestsOption) || forwardedMinimumExpectedTests))
        {
            throw new GracefulException(CliCommandStrings.CmdCollectTestMapCannotRequireMinimumTests);
        }
    }

    internal static (bool CollectTestMap, bool AffectedTests, bool MinimumExpectedTests) DetectAffectedTestsOptionsInForwardedResponseFiles(
        ImmutableArray<string> testApplicationArguments,
        IEnumerable<string?> testApplicationWorkingDirectories,
        string invocationWorkingDirectory)
    {
        var workingDirectories = testApplicationWorkingDirectories
            .Select(directory => string.IsNullOrEmpty(directory)
                ? invocationWorkingDirectory
                : Path.GetFullPath(directory, invocationWorkingDirectory))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        ForwardedOptionState? commonState = null;
        bool foundInvalidResponseFile = false;
        foreach (string workingDirectory in workingDirectories)
        {
            ForwardedOptionState workingDirectoryState = default;
            foreach (string argument in testApplicationArguments)
            {
                if (argument.Length > 1 && argument[0] == '@')
                {
                    if (!TryDetectAffectedTestsOptionsInResponseFile(
                        argument[1..],
                        workingDirectory,
                        new HashSet<string>(StringComparer.Ordinal),
                        out ForwardedOptionState responseFileState))
                    {
                        foundInvalidResponseFile = true;
                        continue;
                    }

                    workingDirectoryState = workingDirectoryState.Merge(responseFileState);
                }
            }

            if (commonState is { } previousState &&
                (previousState.CollectTestMap != workingDirectoryState.CollectTestMap ||
                 previousState.AffectedTests != workingDirectoryState.AffectedTests))
            {
                throw new GracefulException(CliCommandStrings.CmdAffectedTestsResponseFilesMustBeConsistent);
            }

            commonState = workingDirectoryState with
            {
                MinimumExpectedTests =
                    (commonState?.MinimumExpectedTests ?? false) || workingDirectoryState.MinimumExpectedTests,
            };
        }

        ForwardedOptionState state = commonState ?? default;
        if (foundInvalidResponseFile && (state.CollectTestMap || state.AffectedTests))
        {
            throw new GracefulException(CliCommandStrings.CmdAffectedTestsResponseFilesMustBeConsistent);
        }

        if (foundInvalidResponseFile)
        {
            // MTP will report the response-file error. Do not partially activate a mode
            // or replace its diagnostic with an SDK validation error.
            return default;
        }

        return (state.CollectTestMap, state.AffectedTests, state.MinimumExpectedTests);
    }

    private static bool TryDetectAffectedTestsOptionsInResponseFile(
        string responseFilePath,
        string workingDirectory,
        HashSet<string> recursionStack,
        out ForwardedOptionState state)
    {
        state = default;
        string fullPath = Path.GetFullPath(responseFilePath, workingDirectory);
        if (!recursionStack.Add(fullPath) || !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            string[] tokens = [..
                File.ReadAllLines(fullPath)
                    .Select(static line => line.Trim())
                    .Where(static line => line.Length > 0 && line[0] != '#')
                    .SelectMany(SplitResponseFileLine)];

            ForwardedOptionState detectedState = default;
            foreach (string token in tokens)
            {
                if (token.Length > 1 && token[0] == '@')
                {
                    if (!TryDetectAffectedTestsOptionsInResponseFile(
                        token[1..],
                        workingDirectory,
                        recursionStack,
                        out ForwardedOptionState nestedState))
                    {
                        return false;
                    }

                    detectedState = detectedState.Merge(nestedState);
                }
                else
                {
                    if (IsAffectedTestsOption(token, TestCommandDefinition.MicrosoftTestingPlatform.CollectTestMapOptionName))
                    {
                        detectedState = detectedState with { CollectTestMap = true };
                    }
                    else if (IsAffectedTestsOption(token, TestCommandDefinition.MicrosoftTestingPlatform.AffectedTestsOptionName))
                    {
                        detectedState = detectedState with { AffectedTests = true };
                    }
                    else if (IsOption(token, MinimumExpectedTestsOptionName, allowValue: true))
                    {
                        detectedState = detectedState with { MinimumExpectedTests = true };
                    }
                }
            }

            state = detectedState;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // MTP reports response-file read and format errors. Do not replace its diagnostic here.
            return false;
        }
        finally
        {
            recursionStack.Remove(fullPath);
        }
    }

    private static bool HasForwardedOption(ImmutableArray<string> arguments, string canonicalOption)
        => arguments.Any(argument => IsOption(argument, canonicalOption, allowValue: true));

    private static bool IsAffectedTestsOption(string argument, string canonicalOption)
        => IsOption(argument, canonicalOption, allowValue: false);

    private static bool IsOption(string argument, string canonicalOption, bool allowValue)
    {
        if (argument.Length < 2 ||
            argument[0] != '-' ||
            (argument[1] == '-' && (argument.Length < 3 || argument[2] == '-')))
        {
            return false;
        }

        string option = argument[1] == '-' ? argument[2..] : argument[1..];
        int separatorIndex = option.IndexOfAny('=', ':');
        if (separatorIndex >= 0 && !allowValue)
        {
            return false;
        }

        ReadOnlySpan<char> optionName = separatorIndex >= 0 ? option.AsSpan(0, separatorIndex) : option;
        return optionName.Equals(canonicalOption.AsSpan().TrimStart('-'), StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitResponseFileLine(string line)
    {
        int tokenStart = 0;
        int position = 0;
        bool seekingTokenStart = true;
        bool insideQuotes = false;

        while (position < line.Length)
        {
            char character = line[position];

            if (char.IsWhiteSpace(character))
            {
                if (!insideQuotes)
                {
                    if (!seekingTokenStart)
                    {
                        yield return CurrentToken();
                        tokenStart = position;
                        seekingTokenStart = true;
                    }
                    else
                    {
                        tokenStart = position;
                    }
                }
            }
            if (character == '"')
            {
                if (seekingTokenStart)
                {
                    if (insideQuotes)
                    {
                        yield return CurrentToken();
                        tokenStart = position;
                        insideQuotes = false;
                    }
                    else
                    {
                        tokenStart = position + 1;
                        insideQuotes = true;
                    }
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (seekingTokenStart && !insideQuotes && !char.IsWhiteSpace(character))
            {
                seekingTokenStart = false;
                tokenStart = position;
            }

            position++;

            if (position == line.Length)
            {
                if (insideQuotes)
                {
                    throw new FormatException();
                }

                if (!seekingTokenStart)
                {
                    yield return CurrentToken();
                }
            }
        }

        string CurrentToken() => line.Substring(tokenStart, position - tokenStart).Replace("\"", string.Empty);
    }

    private readonly record struct ForwardedOptionState(
        bool CollectTestMap,
        bool AffectedTests,
        bool MinimumExpectedTests)
    {
        public ForwardedOptionState Merge(ForwardedOptionState other)
            => new(
                CollectTestMap || other.CollectTestMap,
                AffectedTests || other.AffectedTests,
                MinimumExpectedTests || other.MinimumExpectedTests);
    }

    internal static bool ShouldFailForNoExecutedTests(bool isAffectedTestsMode, int totalTests, int skippedTests)
        => (!isAffectedTestsMode && totalTests == 0) ||
            (totalTests > 0 && totalTests == skippedTests);

    private static TestListFormat GetListTestsFormat(ParseResult parseResult, TestCommandDefinition.MicrosoftTestingPlatform definition)
    {
        // '--list-tests' has ZeroOrOne arity. A bare '--list-tests' (no value) defaults to text.
        // The accepted values are constrained to 'text'/'json' by the option definition.
        string? value = parseResult.GetValue(definition.ListTestsOption);
        return string.Equals(value, TestCommandDefinition.MicrosoftTestingPlatform.ListTestsFormatJson, StringComparison.Ordinal)
            ? TestListFormat.Json
            : TestListFormat.Text;
    }

    private static TerminalTestReporter InitializeOutput(int degreeOfParallelism, ParseResult parseResult, TestOptions testOptions)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        var console = new SystemConsole();
        var showPassedTests = parseResult.GetValue(definition.OutputOption) == OutputOptions.Detailed;
        var noProgress = parseResult.HasOption(definition.NoProgressOption);
        var noAnsi = parseResult.HasOption(definition.NoAnsiOption);

        // When emitting machine-readable JSON discovery output, stdout must contain only the JSON
        // document. Force off ANSI, progress rendering and the per-assembly "Discovering tests from..."
        // banners so nothing else is interleaved with the JSON.
        bool isJsonDiscovery = testOptions.IsDiscovery && testOptions.ListTestsFormat == TestListFormat.Json;
        if (isJsonDiscovery)
        {
            noProgress = true;
            noAnsi = true;
        }

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
            ShowAssembly = !isJsonDiscovery,
            ShowAssemblyStartAndComplete = !isJsonDiscovery,
            MinimumExpectedTests = parseResult.GetValue(definition.MinimumExpectedTestsOption),
            AllowZeroTests = testOptions.IsAffectedTestsMode,
            ListTestsFormat = testOptions.ListTestsFormat,
            SlowestTestsCount = GetSlowestTestsCount(parseResult.GetArguments()),
            ShowFlakyTests = GetShowFlakyTests(parseResult.GetArguments()),
        });

        // Ctrl+C handling is wired in Run() through CtrlCCancellationManager so that
        // a second press can force-kill running test app child processes and exit with
        // ExitCode.TestSessionAborted (see issue https://github.com/dotnet/sdk/issues/50732).

        // This is ugly, and we need to replace it by passing out some info from testing platform to inform us that some process level retry plugin is active.
        var isRetry = parseResult.GetArguments().Contains("--retry-failed-tests");

        output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, testOptions.IsDiscovery, testOptions.IsHelp, isRetry);
        return output;
    }

    /// <summary>
    /// Reads the Microsoft.Testing.Platform <c>--show-slowest-tests N</c> option out of the raw command line.
    /// </summary>
    /// <remarks>
    /// The option belongs to the test application, not to the 'dotnet test' CLI, so it is forwarded verbatim. Under
    /// the pipe protocol the test host's own terminal reporter is not plugged in (the SDK owns user-facing output),
    /// so the section has to be rendered by the SDK's reporter instead — which means the SDK has to observe the
    /// option. Same approach as the '--retry-failed-tests' detection above. A missing, non-numeric or non-positive
    /// argument leaves the section off, mirroring the upstream option validator.
    /// </remarks>
    internal static int GetSlowestTestsCount(IReadOnlyList<string> arguments)
    {
        for (int i = 0; i < arguments.Count; i++)
        {
            if (!string.Equals(arguments[i], "--show-slowest-tests", StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 < arguments.Count &&
                int.TryParse(arguments[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) &&
                count >= 1)
            {
                return count;
            }

            return 0;
        }

        return 0;
    }

    /// <summary>
    /// Reads the Microsoft.Testing.Platform <c>--show-flaky-tests [on|off]</c> option out of the raw command line.
    /// A bare '--show-flaky-tests' means "on", which is also the default when the option is absent.
    /// See <see cref="GetSlowestTestsCount"/> for why the SDK inspects the forwarded arguments.
    /// </summary>
    internal static bool GetShowFlakyTests(IReadOnlyList<string> arguments)
    {
        for (int i = 0; i < arguments.Count; i++)
        {
            if (!string.Equals(arguments[i], "--show-flaky-tests", StringComparison.Ordinal))
            {
                continue;
            }

            return i + 1 >= arguments.Count || !IsOffValue(arguments[i + 1]);
        }

        return true;

        static bool IsOffValue(string argument)
            => string.Equals(argument, "off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "disable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "0", StringComparison.Ordinal);
    }

    private static int GetDegreeOfParallelism(ParseResult parseResult, bool collectTestMap)
    {
        if (collectTestMap)
        {
            return 1;
        }

        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        var degreeOfParallelism = parseResult.GetValue(definition.MaxParallelTestModulesOption);
        if (degreeOfParallelism <= 0)
            degreeOfParallelism = Environment.ProcessorCount;
        return degreeOfParallelism;
    }

    /// <summary>
    /// Creates the MSBuild session shared by every target the test command invokes itself. It owns the
    /// project collection all those projects have to be evaluated in; the collection has no global
    /// properties of its own, so each project passes the ones it needs when it is evaluated.
    /// </summary>
    private static MSBuildSession CreateBuildSession(BuildOptions buildOptions, FacadeLogger? logger)
        => new(SolutionAndProjectUtility.AnalyzeStandardTestMSBuildArgs(buildOptions.MSBuildArgs), logger);

    /// <summary>
    /// When --device is specified, we need to ensure a single target framework is selected
    /// because a device is platform-specific. If -f/--framework wasn't provided, this method
    /// evaluates the project to get TargetFrameworks and prompts for selection.
    /// The selected device is also added to MSBuild args so the build sees it.
    /// Solutions are rejected because each project may have its own device list, so
    /// applying a single --device value across a solution is ambiguous.
    /// </summary>
    private static BuildOptions HandleDeviceWithTargetFrameworkSelection(BuildOptions buildOptions, FacadeLogger? logger)
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
            using var _ = MSBuildForwardingAppWithoutLogging.SetMSBuildRequiredEnvironmentVariables();

            // Evaluate the project to get TargetFrameworks
            using var collection = new ProjectCollection(
                globalProperties,
                logger is null ? null : [logger],
                ToolsetDefinitionLocations.Default);
            var projectInstance = ProjectInstance.FromFile(projectPath, new ProjectOptions
            {
                GlobalProperties = globalProperties,
                EvaluationStage = ProjectEvaluationStage.Properties,
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
    private static int HandleListDevices(BuildOptions buildOptions, MSBuildSession buildSession)
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
            buildOptions.EnvironmentVariables,
            commandName: "dotnet test",
            binaryLogger: null,
            buildSession: buildSession);

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

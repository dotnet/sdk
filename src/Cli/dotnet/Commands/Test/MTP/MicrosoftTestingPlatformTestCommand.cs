// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal partial class MicrosoftTestingPlatformTestCommand
{
    private TerminalTestReporter? _output;

    public int Run(ParseResult parseResult, bool isHelp)
    {
        int? exitCode = null;
        try
        {
            exitCode = RunInternal(parseResult, isHelp);
            return exitCode.Value;
        }
        finally
        {
            _output?.TestExecutionCompleted(DateTimeOffset.Now, exitCode);
        }
    }

    private int RunInternal(ParseResult parseResult, bool isHelp)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        BuildOptions buildOptions = MSBuildUtility.GetBuildOptions(parseResult);
        ValidationUtility.ValidateMutuallyExclusiveOptions(parseResult, buildOptions.PathOptions);

        int degreeOfParallelism = GetDegreeOfParallelism(parseResult);
        var testOptions = new TestOptions(
            IsHelp: isHelp,
            IsDiscovery: parseResult.HasOption(definition.ListTestsOption),
            EnvironmentVariables: parseResult.GetValue(definition.EnvOption) ?? ImmutableDictionary<string, string>.Empty);


        bool filterModeEnabled = parseResult.HasOption(definition.TestModulesFilterOption);
        TestApplicationActionQueue actionQueue;
        if (buildOptions.PathOptions.TestModules is not null)
        {
            InitializeOutput(degreeOfParallelism, parseResult, testOptions);

            actionQueue = new TestApplicationActionQueue(degreeOfParallelism, buildOptions, testOptions, _output, OnHelpRequested);
            var testModulesFilterHandler = new TestModulesFilterHandler(actionQueue, _output);
            if (!testModulesFilterHandler.RunWithTestModulesFilter(parseResult, buildOptions.PathOptions.TestModules))
            {
                return ExitCode.GenericFailure;
            }
        }
        else
        {
            var msBuildHandler = new MSBuildHandler(buildOptions);
            if (!msBuildHandler.RunMSBuild())
            {
                return ExitCode.GenericFailure;
            }

            InitializeOutput(degreeOfParallelism, parseResult, testOptions);

            // NOTE: Don't create TestApplicationActionQueue before RunMSBuild.
            // The constructor will do Task.Run calls matching the degree of parallelism, and if we did that before the build, that can
            // be slowing us down unnecessarily.
            // Alternatively, if we can enqueue right after every project evaluation without waiting all evaluations to be done, we can enqueue early.
            actionQueue = new TestApplicationActionQueue(degreeOfParallelism, buildOptions, testOptions, _output, OnHelpRequested);
            if (!msBuildHandler.EnqueueTestApplications(actionQueue))
            {
                return ExitCode.GenericFailure;
            }
        }

        actionQueue.EnqueueCompleted();
        // Don't inline exitCode variable. We want to always call WaitAllActions first.
        var exitCode = actionQueue.WaitAllActions();

        // If all test apps exited with 0 exit code, but we detected that handshake didn't happen correctly, map that to generic failure.
        if (exitCode == ExitCode.Success && _output.HasHandshakeFailure)
        {
            exitCode = ExitCode.GenericFailure;
        }

        if (exitCode == ExitCode.Success &&
            parseResult.HasOption(definition.MinimumExpectedTestsOption) &&
            parseResult.GetValue(definition.MinimumExpectedTestsOption) is { } minimumExpectedTests &&
            _output.TotalTests < minimumExpectedTests)
        {
            exitCode = ExitCode.MinimumExpectedTestsPolicyViolation;
        }

        return exitCode;
    }

    [MemberNotNull(nameof(_output))]
    private void InitializeOutput(int degreeOfParallelism, ParseResult parseResult, TestOptions testOptions)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        var console = new SystemConsole();
        var showPassedTests = parseResult.GetValue(definition.OutputOption) == OutputOptions.Detailed;
        var noProgress = parseResult.HasOption(definition.NoProgressOption);
        var noAnsi = parseResult.HasOption(definition.NoAnsiOption);

        // TODO: Replace this with proper CI detection that we already have in telemetry. https://github.com/microsoft/testfx/issues/5533#issuecomment-2838893327
        bool inCI = string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        AnsiMode ansiMode = AnsiMode.AnsiIfPossible;
        if (noAnsi)
        {
            // User explicitly specified --no-ansi.
            // We should respect that.
            ansiMode = AnsiMode.NoAnsi;
        }
        else if (inCI)
        {
            ansiMode = AnsiMode.SimpleAnsi;
        }

        _output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
        {
            ShowPassedTests = showPassedTests,
            ShowProgress = !noProgress,
            AnsiMode = ansiMode,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
            MinimumExpectedTests = parseResult.GetValue(definition.MinimumExpectedTestsOption),
        });

        Console.CancelKeyPress += (s, e) =>
        {
            _output.StartCancelling();
        };

        // This is ugly, and we need to replace it by passing out some info from testing platform to inform us that some process level retry plugin is active.
        var isRetry = parseResult.GetArguments().Contains("--retry-failed-tests");

        _output.TestExecutionStarted(DateTimeOffset.Now, degreeOfParallelism, testOptions.IsDiscovery, testOptions.IsHelp, isRetry);
    }

    private static int GetDegreeOfParallelism(ParseResult parseResult)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        var degreeOfParallelism = parseResult.GetValue(definition.MaxParallelTestModulesOption);
        if (degreeOfParallelism <= 0)
            degreeOfParallelism = Environment.ProcessorCount;
        return degreeOfParallelism;
    }
}

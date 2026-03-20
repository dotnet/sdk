// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal partial class MicrosoftTestingPlatformTestCommand
{
    public int Run(ParseResult parseResult, bool isHelp)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        BuildOptions buildOptions = MSBuildUtility.GetBuildOptions(parseResult);
        ValidationUtility.ValidateMutuallyExclusiveOptions(parseResult, buildOptions.PathOptions);

        ITestHandler testHandler = buildOptions.PathOptions.TestModules is { } testModules
            ? new TestModulesFilterHandler(testModules, parseResult)
            : new MSBuildHandler(buildOptions);

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
        int? exitCode = null;
        try
        {
            var actionQueue = new TestApplicationActionQueue(degreeOfParallelism, buildOptions, testOptions, output, OnHelpRequested);
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

        var output = new TerminalTestReporter(console, new TerminalTestReporterOptions()
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
            output.StartCancelling();
        };

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
}

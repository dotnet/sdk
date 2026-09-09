// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
#if !CLI_AOT
using Microsoft.DotNet.FileBasedPrograms;
#endif

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestCommandOptions
{
    public static BuildOptions GetBuildOptions(ParseResult parseResult)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        ImmutableArray<string> unmatchedTokens = [.. parseResult.UnmatchedTokens];
        ImmutableArray<string> otherArgs;
#if CLI_AOT
        // Eligibility requires an explicit test-module expression and permits test-application
        // arguments only after the separator, so no positional project/module inference is needed.
        if (!CommonRunHelpers.TrySplitApplicationArgumentsAtDoubleDash(
            parseResult,
            unmatchedTokens,
            out int unmatchedTokenCountBeforeDoubleDash,
            out string[] argumentsAfterDoubleDash)
            || unmatchedTokenCountBeforeDoubleDash != 0)
        {
            otherArgs = [];
        }
        else
        {
            otherArgs = [.. argumentsAfterDoubleDash];
        }

        IEnumerable<string> msbuildArgs = [];
#else
        ImmutableArray<string> loggerArgs;
        int positionalArgumentCount;
        if (CommonRunHelpers.TrySplitApplicationArgumentsAtDoubleDash(
            parseResult,
            unmatchedTokens,
            out int unmatchedTokenCountBeforeDoubleDash,
            out string[] argumentsAfterDoubleDash))
        {
            LoggerUtility.SeparateMSBuildArguments(
                unmatchedTokens[..unmatchedTokenCountBeforeDoubleDash],
                out loggerArgs,
                out var argumentsBeforeDoubleDash);
            positionalArgumentCount = argumentsBeforeDoubleDash.Length;
            otherArgs = [.. argumentsBeforeDoubleDash, .. argumentsAfterDoubleDash];
        }
        else
        {
            LoggerUtility.SeparateMSBuildArguments(unmatchedTokens, out loggerArgs, out otherArgs);
            positionalArgumentCount = otherArgs.Length;
        }
#endif

        if (parseResult.GetValue(definition.NoLogoOption) && !otherArgs.Contains("--no-banner"))
        {
            otherArgs = otherArgs.Add("--no-banner");
        }

#if !CLI_AOT
        var (positionalProjectOrSolution, positionalTestModules) = GetPositionalArguments(
            positionalArgumentCount,
            ref otherArgs);

        IEnumerable<string> msbuildArgs = parseResult.OptionValuesToBeForwarded(definition)
            .Concat(loggerArgs);
#endif

        string? resultsDirectory = parseResult.GetValue(definition.ResultsDirectoryOption);
        if (resultsDirectory is not null)
        {
            resultsDirectory = Path.GetFullPath(resultsDirectory);
        }

        string? configFile = parseResult.GetValue(definition.ConfigFileOption);
        if (configFile is not null)
        {
            configFile = Path.GetFullPath(configFile);
        }

        string? diagnosticOutputDirectory = parseResult.GetValue(definition.DiagnosticOutputDirectoryOption);
        if (diagnosticOutputDirectory is not null)
        {
            diagnosticOutputDirectory = Path.GetFullPath(diagnosticOutputDirectory);
        }

        string? projectOrSolutionOptionValue = parseResult.GetValue(definition.ProjectOrSolutionOption);
        string? testModulesFilterOptionValue = parseResult.GetValue(definition.TestModulesFilterOption);

#if !CLI_AOT
        if ((projectOrSolutionOptionValue is not null && positionalProjectOrSolution is not null) ||
            (testModulesFilterOptionValue is not null && positionalTestModules is not null))
        {
            throw new GracefulException(CliCommandStrings.CmdMultipleBuildPathOptionsErrorDescription);
        }
#endif

        PathOptions pathOptions = new(
#if CLI_AOT
            projectOrSolutionOptionValue,
#else
            positionalProjectOrSolution ?? projectOrSolutionOptionValue,
#endif
            parseResult.GetValue(definition.SolutionOption),
#if CLI_AOT
            testModulesFilterOptionValue,
#else
            positionalTestModules ?? testModulesFilterOptionValue,
#endif
            resultsDirectory,
            parseResult.GetValue(definition.ResultsDirectoryLayoutOption) == "per-module"
                ? ResultsDirectoryLayout.PerModule
                : ResultsDirectoryLayout.Flat,
            configFile,
            diagnosticOutputDirectory,
            parseResult.HasOption(definition.ResultsDirectoryLayoutOption));

        return new BuildOptions(
            pathOptions,
            parseResult.GetValue(definition.NoRestoreOption),
            parseResult.GetValue(definition.NoBuildOption),
            parseResult.HasOption(definition.VerbosityOption) ? parseResult.GetValue(definition.VerbosityOption) : null,
            parseResult.GetValue(definition.NoLaunchProfileOption),
            parseResult.GetValue(definition.NoLaunchProfileArgumentsOption),
            otherArgs,
            msbuildArgs,
            Device: parseResult.GetValue(definition.DeviceOption),
            ListDevices: parseResult.GetValue(definition.ListDevicesOption),
            EnvironmentVariables: parseResult.GetValue(definition.EnvOption) ?? ImmutableDictionary<string, string>.Empty);
    }

#if !CLI_AOT
    private static (string? PositionalProjectOrSolution, string? PositionalTestModules) GetPositionalArguments(
        int positionalArgumentCount,
        ref ImmutableArray<string> otherArgs)
    {
        string? positionalProjectOrSolution = null;
        string? positionalTestModules = null;

        // This validation only improves diagnostics for inputs that would otherwise fail. Users can
        // disable it for a valid extension-specific scenario.
        bool throwOnUnexpectedFilePassedAsNonFirstPositionalArgument =
            Environment.GetEnvironmentVariable("DOTNET_TEST_DISABLE_SWITCH_VALIDATION") is not ("true" or "1");

        for (int i = 0; i < positionalArgumentCount; i++)
        {
            string token = otherArgs[i];
            if ((token.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)) && File.Exists(token))
            {
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseSolution);
                }
            }
            else if (Path.GetExtension(token).EndsWith("proj", StringComparison.OrdinalIgnoreCase) && File.Exists(token))
            {
                // Accept every MSBuild project extension ending in "proj", matching project-path validation.
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseProject);
                }
            }
            else if (VirtualProjectBuilder.IsValidEntryPointPath(token, requireFileToExist: i != 0))
            {
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseProject);
                }
            }
            else if ((token.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                      token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) &&
                     File.Exists(token))
            {
                if (i == 0)
                {
                    positionalTestModules = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseTestModules);
                }
            }
            else if (Directory.Exists(token))
            {
                if (i == 0)
                {
                    positionalProjectOrSolution = token;
                    otherArgs = otherArgs.RemoveAt(0);
                    break;
                }
                else if (throwOnUnexpectedFilePassedAsNonFirstPositionalArgument)
                {
                    throw new GracefulException(CliCommandStrings.TestCommandUseDirectoryWithSwitch);
                }
            }
        }

        return (positionalProjectOrSolution, positionalTestModules);
    }
#endif
}

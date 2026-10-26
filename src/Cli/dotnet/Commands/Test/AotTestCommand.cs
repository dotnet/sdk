// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if CLI_AOT
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Handles build-free Microsoft.Testing.Platform invocations inside the Native AOT CLI.
/// </summary>
internal static class AotTestCommand
{
    private const string DiagnosticsOptionName = "--diagnostics";

    internal static void ConfigureCommand(TestCommandDefinition command)
    {
        if (command is TestCommandDefinition.MicrosoftTestingPlatform mtp)
        {
            mtp.SetAction(Execute);
        }
    }

    internal static int Execute(ParseResult parseResult)
    {
        if (parseResult.CommandResult.Command is not TestCommandDefinition.MicrosoftTestingPlatform definition
            || string.IsNullOrWhiteSpace(parseResult.GetValue(definition.TestModulesFilterOption))
            || HasUnsupportedRootOption(parseResult)
            || GetUnsupportedOption(parseResult, definition) is not null
            || HasUnmatchedTokenBeforeDoubleDash(parseResult))
        {
            throw new CommandNotAvailableInAotException();
        }

        Reporter.Verbose.WriteLine("AOT test tier: TestModules.");
        return new MicrosoftTestingPlatformTestCommand().Run(parseResult, isHelp: false);
    }

    private static bool HasUnsupportedRootOption(ParseResult parseResult)
        => parseResult.RootCommandResult.Children
            .OfType<OptionResult>()
            .Any(optionResult =>
                !optionResult.Implicit
                && optionResult.Option.Name == DiagnosticsOptionName);

    private static bool HasUnmatchedTokenBeforeDoubleDash(ParseResult parseResult)
    {
        string[] unmatchedTokens = [.. parseResult.UnmatchedTokens];
        if (unmatchedTokens.Length == 0)
        {
            return false;
        }

        return !CommonRunHelpers.TrySplitApplicationArgumentsAtDoubleDash(
            parseResult,
            unmatchedTokens,
            out int unmatchedTokenCountBeforeDoubleDash,
            out _)
            || unmatchedTokenCountBeforeDoubleDash != 0;
    }

    private static Option? GetUnsupportedOption(
        ParseResult parseResult,
        TestCommandDefinition.MicrosoftTestingPlatform definition)
        => parseResult.CommandResult.Children
            .OfType<OptionResult>()
            .FirstOrDefault(optionResult =>
                !optionResult.Implicit
                && optionResult.Option != definition.TestModulesFilterOption
                && optionResult.Option != definition.TestModulesRootDirectoryOption
                && optionResult.Option != definition.ResultsDirectoryOption
                && optionResult.Option != definition.ResultsDirectoryLayoutOption
                && optionResult.Option != definition.ConfigFileOption
                && optionResult.Option != definition.DiagnosticOutputDirectoryOption
                && optionResult.Option != definition.MaxParallelTestModulesOption
                && optionResult.Option != definition.MinimumExpectedTestsOption
                && optionResult.Option != definition.MaximumFailedTestsOption
                && optionResult.Option != definition.TimeoutOption
                && optionResult.Option != definition.EnvOption
                && optionResult.Option != definition.NoRestoreOption
                && optionResult.Option != definition.NoBuildOption
                && optionResult.Option != definition.NoLogoOption
                && optionResult.Option != definition.NoAnsiOption
                && optionResult.Option != definition.NoProgressOption
                && optionResult.Option != definition.NoArtifactPostProcessingOption
                && optionResult.Option != definition.OutputOption
                && optionResult.Option != definition.ListTestsOption
                && optionResult.Option != definition.NoLaunchProfileOption
                && optionResult.Option != definition.NoLaunchProfileArgumentsOption
                && optionResult.Option != definition.CollectTestMapOption
                && optionResult.Option != definition.AffectedTestsOption)
            ?.Option;
}
#endif

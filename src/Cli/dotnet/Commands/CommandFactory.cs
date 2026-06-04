// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands;

public static class CommandFactory
{
    internal static CommandBase CreateVirtualOrPhysicalCommand(
        System.CommandLine.Command commandDefinition,
        Argument<string[]> catchAllUserInputArgument,
        Func<MSBuildArgs, string, VirtualProjectBuildingCommand> createVirtualCommand,
        Func<MSBuildArgs, string?, CommandBase> createPhysicalCommand,
        IEnumerable<Option> optionsToUseWhenParsingMSBuildFlags,
        ParseResult parseResult,
        string? msbuildPath = null,
        Func<MSBuildArgs, MSBuildArgs>? transformer = null)
    {
        var args = parseResult.GetValue(catchAllUserInputArgument) ?? [];
        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);
        var forwardedArgs = parseResult.OptionValuesToBeForwarded(commandDefinition);
        if (nonBinLogArgs is [{ } arg] && VirtualProjectBuilder.IsValidEntryPointPath(arg))
        {
            var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([.. forwardedArgs, .. binLogArgs],
            [
                .. optionsToUseWhenParsingMSBuildFlags,
                CommonOptions.CreateGetPropertyOption(),
                CommonOptions.CreateGetItemOption(),
                CommonOptions.CreateGetTargetResultOption(),
                CommonOptions.CreateGetResultOutputFileOption(),
            ]);
            msbuildArgs = transformer?.Invoke(msbuildArgs) ?? msbuildArgs;
            return createVirtualCommand(msbuildArgs, Path.GetFullPath(arg));
        }
        else
        {
            // Warn if any argument looks like a file-based program entry point but we're falling back to MSBuild.
            // This can happen when extra positional arguments prevent the single-arg file-based path from being taken,
            // or when a .cs file doesn't exist (so IsValidEntryPointPath returns false).
            foreach (var candidate in nonBinLogArgs)
            {
                if (VirtualProjectBuilder.IsValidEntryPointPath(candidate))
                {
                    Reporter.Error.WriteLine(
                        string.Format(
                            CliCommandStrings.WarningFileArgumentPassedToMSBuild,
                            candidate,
                            commandDefinition.Name,
                            FormatUnsupportedArguments(nonBinLogArgs, candidate)).Yellow());
                    break;
                }

                if (candidate.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    Reporter.Error.WriteLine(
                        string.Format(
                            CliCommandStrings.WarningCsFileArgumentPassedToMSBuild,
                            candidate,
                            FormatUnrecognizedArguments(nonBinLogArgs)).Yellow());
                    break;
                }
            }

            var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([.. forwardedArgs, .. args], [.. optionsToUseWhenParsingMSBuildFlags]);
            msbuildArgs = transformer?.Invoke(msbuildArgs) ?? msbuildArgs;
            return createPhysicalCommand(msbuildArgs, msbuildPath);
        }
    }

    private static string FormatUnsupportedArguments(IEnumerable<string> args, string supportedArgument) =>
        FormatArguments(RemoveFirst(args, supportedArgument));

    private static string FormatUnrecognizedArguments(IEnumerable<string> args) => FormatArguments(args);

    private static string FormatArguments(IEnumerable<string> args) => string.Join(", ", args.Select(arg => $"'{arg}'"));

    private static IEnumerable<string> RemoveFirst(IEnumerable<string> args, string value)
    {
        var removed = false;
        foreach (var arg in args)
        {
            if (!removed && string.Equals(arg, value, StringComparison.Ordinal))
            {
                removed = true;
                continue;
            }

            yield return arg;
        }
    }
}

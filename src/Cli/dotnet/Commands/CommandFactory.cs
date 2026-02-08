// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
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
            var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([.. forwardedArgs, .. args], [.. optionsToUseWhenParsingMSBuildFlags]);
            msbuildArgs = transformer?.Invoke(msbuildArgs) ?? msbuildArgs;
            return createPhysicalCommand(msbuildArgs, msbuildPath);
        }
    }
}

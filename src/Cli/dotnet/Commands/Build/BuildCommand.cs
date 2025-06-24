// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Build;

public static class BuildCommand
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var parser = Parser.Instance;
        var parseResult = parser.ParseFrom("dotnet build", args);
        return FromParseResult(parseResult, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult parseResult, string? msbuildPath = null)
    {
        PerformanceLogEventSource.Log.CreateBuildCommandStart();

        parseResult.ShowHelpOrErrorIfAppropriate();

        CommonOptions.ValidateSelfContainedOptions(
            parseResult.GetResult(BuildCommandParser.SelfContainedOption) is not null,
            parseResult.GetResult(BuildCommandParser.NoSelfContainedOption) is not null);

        string[] args = parseResult.GetValue(BuildCommandParser.SlnOrProjectOrFileArgument) ?? [];

        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);

        string[] forwardedOptions = parseResult.OptionValuesToBeForwarded(BuildCommandParser.GetCommand()).ToArray();

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([..forwardedOptions, ..binLogArgs], CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption);

        bool noRestore = parseResult.GetResult(BuildCommandParser.NoRestoreOption) is not null;

        bool noIncremental = parseResult.GetResult(BuildCommandParser.NoIncrementalOption) is not null;

        CommandBase command;

        if (nonBinLogArgs is [{ } arg] && VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
        {
            command = new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(arg),
                msbuildArgs: msbuildArgs
            )
            {
                NoRestore = noRestore,
                NoCache = true,
                BuildTarget = noIncremental ? "Rebuild" : "Build",
            };
        }
        else
        {
            msbuildArgs.OtherMSBuildArgs.AddRange(["-consoleloggerparameters:Summary", .. nonBinLogArgs]);
            if (noIncremental)
            {
                msbuildArgs.OtherMSBuildArgs.Add("-target:Rebuild");
            }

            command = new RestoringCommand(
                msbuildArgs: msbuildArgs,
                noRestore: noRestore,
                msbuildPath: msbuildPath);
        }

        PerformanceLogEventSource.Log.CreateBuildCommandStop();

        return command;
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

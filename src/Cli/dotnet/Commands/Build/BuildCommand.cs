// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Build;

public static class BuildCommand
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var parseResult = Parser.Parse(["dotnet", "build", ..args]);
        return FromParseResult(parseResult, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult parseResult, string? msbuildPath = null)
    {
        parseResult.ShowHelpOrErrorIfAppropriate();

        CommonOptions.ValidateSelfContainedOptions(
            parseResult.HasOption(BuildCommandParser.SelfContainedOption),
            parseResult.HasOption(BuildCommandParser.NoSelfContainedOption));

        bool noRestore = parseResult.HasOption(BuildCommandParser.NoRestoreOption);

        return CommandFactory.CreateVirtualOrPhysicalCommand(
            BuildCommandParser.GetCommand(),
            BuildCommandParser.SlnOrProjectOrFileArgument,
            (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(appFilePath),
                msbuildArgs: msbuildArgs
            )
            {
                NoRestore = noRestore,
                NoCache = true,
            },
            (msbuildArgs, msbuildPath) => new RestoringCommand(
                msbuildArgs: msbuildArgs.CloneWithAdditionalArgs("-consoleloggerparameters:Summary"),
                noRestore: noRestore,
                msbuildPath: msbuildPath
            ),
            [CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, BuildCommandParser.TargetOption],
            parseResult,
            msbuildPath
        );
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
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
        var definition = (BuildCommandDefinition)parseResult.CommandResult.Command;

        parseResult.ShowHelpOrErrorIfAppropriate();

        CommonOptions.ValidateSelfContainedOptions(
            parseResult.HasOption(definition.SelfContainedOption),
            parseResult.HasOption(definition.NoSelfContainedOption));

        bool noRestore = parseResult.HasOption(definition.NoRestoreOption);

        return CommandFactory.CreateVirtualOrPhysicalCommand(
            definition,
            definition.SlnOrProjectOrFileArgument,
            createVirtualCommand: (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(appFilePath),
                msbuildArgs: msbuildArgs)
            {
                NoRestore = noRestore,
                NoCache = true,
            },
            createPhysicalCommand: (msbuildArgs, msbuildPath) => new RestoringCommand(
                msbuildArgs: msbuildArgs.CloneWithAdditionalArgs("-consoleloggerparameters:Summary"),
                noRestore: noRestore,
                msbuildPath: msbuildPath
            ),
            optionsToUseWhenParsingMSBuildFlags:
            [
                CommonOptions.CreatePropertyOption(),
                CommonOptions.CreateRestorePropertyOption(),
                CommonOptions.CreateMSBuildTargetOption(),
                CommonOptions.CreateVerbosityOption(),
                CommonOptions.CreateNoLogoOption()
            ],
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

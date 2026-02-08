// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Clean;

public sealed class CleanCommand(MSBuildArgs msbuildArgs, string? msbuildPath = null) : MSBuildForwardingApp(msbuildArgs, msbuildPath)
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var result = Parser.Parse(["dotnet", "clean", ..args]);
        return FromParseResult(result, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult result, string? msbuildPath = null)
    {
        var definition = (CleanCommandDefinition)result.CommandResult.Command;

        result.ShowHelpOrErrorIfAppropriate();
        return CommandFactory.CreateVirtualOrPhysicalCommand(
            definition,
            definition.SlnOrProjectOrFileArgument,
            createVirtualCommand: static (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: appFilePath,
                msbuildArgs: msbuildArgs)
            {
                NoBuild = false,
                NoRestore = true,
                NoCache = true,
                NoWriteBuildMarkers = true,
            },
            createPhysicalCommand: static (msbuildArgs, msbuildPath) => new CleanCommand(msbuildArgs, msbuildPath),
            optionsToUseWhenParsingMSBuildFlags:
            [
                CommonOptions.CreatePropertyOption(),
                CommonOptions.CreateRestorePropertyOption(),
                CleanCommandDefinition.CreateTargetOption(),
                CommonOptions.CreateVerbosityOption(VerbosityOptions.normal),
                CommonOptions.CreateNoLogoOption()
            ],
            result,
            msbuildPath
        );
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();
        return FromParseResult(parseResult).Execute();
    }
}

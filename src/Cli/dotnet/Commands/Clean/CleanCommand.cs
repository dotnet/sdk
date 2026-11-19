// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Clean;

public sealed class CleanCommand(MSBuildArgs msbuildArgs, string? msbuildPath, CommandServices services) : MSBuildForwardingApp(msbuildArgs, msbuildPath, services)
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath, CommandServices services)
    {
        var result = Parser.Parse(["dotnet", "clean", .. args]);
        return FromParseResult(result, msbuildPath, services);
    }

    public static CommandBase FromParseResult(ParseResult result, string? msbuildPath, CommandServices services)
    {
        var definition = (CleanCommandDefinition)result.CommandResult.Command;

        result.ShowHelpOrErrorIfAppropriate();

        Option[] optionsToUseWhenParsingMSBuildFlags =
        [
            CommonOptions.CreatePropertyOption(),
            CommonOptions.CreateRestorePropertyOption(),
            CleanCommandDefinition.CreateTargetOption(),
            CommonOptions.CreateVerbosityOption(VerbosityOptions.normal),
            CommonOptions.CreateNoLogoOption()
        ];

#if CLI_AOT
        var msbuildArgs = DotNetCommandFactory.AnalyzePhysicalMSBuildArguments(
            result.OptionValuesToBeForwarded(definition),
            result.GetValue(definition.SlnOrProjectOrFileArgument) ?? [],
            optionsToUseWhenParsingMSBuildFlags);
        return new CleanCommand(msbuildArgs, msbuildPath, services);
#else
        return DotNetCommandFactory.CreateVirtualOrPhysicalCommand(
            definition,
            definition.SlnOrProjectOrFileArgument,
            createVirtualCommand: (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: appFilePath,
                msbuildArgs: msbuildArgs,
                services: services)
            {
                NoBuild = false,
                NoRestore = true,
                NoCache = true,
                NoWriteBuildMarkers = true,
            },
            createPhysicalCommand: (msbuildArgs, msbuildPath) => new CleanCommand(msbuildArgs, msbuildPath, services),
            optionsToUseWhenParsingMSBuildFlags,
            result,
            msbuildPath
        );
#endif
    }

    public static int Run(ParseResult parseResult)
    {
#if !CLI_AOT
        parseResult.HandleDebugSwitch();
#endif
        return FromParseResult(parseResult, msbuildPath: null, services: CommandServices.CreateDefault()).Execute();
    }
}

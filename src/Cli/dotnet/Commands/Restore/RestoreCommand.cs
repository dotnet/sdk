// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Restore;

public static class RestoreCommand
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath, CommandServices services)
    {
        var result = Parser.Parse(["dotnet", "restore", .. args]);
        return FromParseResult(result, msbuildPath, services);
    }

    public static CommandBase FromParseResult(ParseResult result, string? msbuildPath, CommandServices services)
    {
        var definition = (RestoreCommandDefinition)result.CommandResult.Command;

#if !CLI_AOT
        result.HandleDebugSwitch();
#endif
        result.ShowHelpOrErrorIfAppropriate();

        Option[] optionsToUseWhenParsingMSBuildFlags =
        [
            CommonOptions.CreatePropertyOption(),
            CommonOptions.CreateRestorePropertyOption(),
            RestoreCommandDefinition.CreateTargetOption(),
            CommonOptions.CreateVerbosityOption(),
            CommonOptions.CreateNoLogoOption()
        ];

#if CLI_AOT
        var msbuildArgs = DotNetCommandFactory.AnalyzePhysicalMSBuildArguments(
            result.OptionValuesToBeForwarded(definition),
            result.GetValue(definition.SlnOrProjectOrFileArgument) ?? [],
            optionsToUseWhenParsingMSBuildFlags);
        return CreateForwarding(msbuildArgs, msbuildPath, services);
#else
        return DotNetCommandFactory.CreateVirtualOrPhysicalCommand(
            definition,
            definition.SlnOrProjectOrFileArgument,
            (msbuildArgs, appFilePath) =>
            {
                return new VirtualProjectBuildingCommand(
                    entryPointFileFullPath: Path.GetFullPath(appFilePath),
                    msbuildArgs: msbuildArgs,
                    services: services
                )
                {
                    NoBuild = true,
                    NoCache = true,
                };
            },
            (msbuildArgs, msbuildPath) =>
            {
                return CreateForwarding(msbuildArgs, msbuildPath, services);
            },
            optionsToUseWhenParsingMSBuildFlags,
            result,
            msbuildPath
        );
#endif
    }

    public static MSBuildForwardingApp CreateForwarding(MSBuildArgs msbuildArgs, string? msbuildPath, CommandServices services)
        => RestoringCommand.CreateRestoreForwarding(msbuildArgs, msbuildPath, services);

    public static int Run(string[] args, CancellationToken cancellationToken)
    {
#if !CLI_AOT
        DebugHelper.HandleDebugSwitch(ref args);
#endif

        return FromArgs(args, msbuildPath: null, services: CommandServices.CreateDefault()).Execute(cancellationToken);
    }

    public static int Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
#if !CLI_AOT
        parseResult.HandleDebugSwitch();
#endif

        return FromParseResult(parseResult, msbuildPath: null, services: CommandServices.CreateDefault()).Execute(cancellationToken);
    }
}

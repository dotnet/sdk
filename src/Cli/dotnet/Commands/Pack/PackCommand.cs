// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Pack;

public class PackCommand(
    MSBuildArgs msbuildArgs,
    bool noRestore,
    string? msbuildPath = null
    ) : RestoringCommand(msbuildArgs, noRestore, msbuildPath: msbuildPath)
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var parseResult = Parser.Parse(["dotnet", "pack", ..args]);
        return FromParseResult(parseResult, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult parseResult, string? msbuildPath = null)
    {
        parseResult.ShowHelpOrErrorIfAppropriate();

        var args = parseResult.GetValue(PackCommandParser.SlnOrProjectOrFileArgument) ?? [];

        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);

        bool noBuild = parseResult.HasOption(PackCommandParser.NoBuildOption);

        bool noRestore = noBuild || parseResult.HasOption(PackCommandParser.NoRestoreOption);

        return CommandFactory.CreateVirtualOrPhysicalCommand(
            PackCommandParser.GetCommand(),
            PackCommandParser.SlnOrProjectOrFileArgument,
            (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(appFilePath),
                msbuildArgs: msbuildArgs)
            {
                NoBuild = noBuild,
                NoRestore = noRestore,
                NoCache = true,
            },
            (msbuildArgs, msbuildPath) =>
            {
                ReleasePropertyProjectLocator projectLocator = new(parseResult, MSBuildPropertyNames.PACK_RELEASE,
                    new ReleasePropertyProjectLocator.DependentCommandOptions(
                            nonBinLogArgs,
                            parseResult.HasOption(PackCommandParser.ConfigurationOption) ? parseResult.GetValue(PackCommandParser.ConfigurationOption) : null
                        )
                );
                return new PackCommand(
                    msbuildArgs.CloneWithAdditionalProperties(projectLocator.GetCustomDefaultConfigurationValueIfSpecified()),
                    noRestore,
                    msbuildPath);
            },
            optionsToUseWhenParsingMSBuildFlags:
            [
                CommonOptions.PropertiesOption,
                CommonOptions.RestorePropertiesOption,
                PackCommandParser.TargetOption,
                PackCommandParser.VerbosityOption,
            ],
            parseResult,
            msbuildPath);
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

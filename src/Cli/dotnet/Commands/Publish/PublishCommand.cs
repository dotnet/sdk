// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Publish;

public class PublishCommand : RestoringCommand
{
    private PublishCommand(
        MSBuildArgs msbuildArgs,
        bool noRestore,
        string? msbuildPath = null)
        : base(msbuildArgs, noRestore, msbuildPath)
    {
    }

    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var parseResult = Parser.Parse(["dotnet", "publish", ..args]);
        return FromParseResult(parseResult);
    }

    public static CommandBase FromParseResult(ParseResult parseResult, string? msbuildPath = null)
    {
        parseResult.HandleDebugSwitch();
        parseResult.ShowHelpOrErrorIfAppropriate();

        string[] args = parseResult.GetValue(PublishCommandParser.SlnOrProjectOrFileArgument) ?? [];

        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);

        CommonOptions.ValidateSelfContainedOptions(parseResult.HasOption(PublishCommandParser.SelfContainedOption),
            parseResult.HasOption(PublishCommandParser.NoSelfContainedOption));

        bool noBuild = parseResult.HasOption(PublishCommandParser.NoBuildOption);

        bool noRestore = noBuild || parseResult.HasOption(PublishCommandParser.NoRestoreOption);

        return CommandFactory.CreateVirtualOrPhysicalCommand(
            PublishCommandParser.GetCommand(),
            PublishCommandParser.SlnOrProjectOrFileArgument,
            (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(appFilePath),
                msbuildArgs: msbuildArgs)
            {
                NoBuild = noBuild,
                NoRestore = noRestore,
                NoCache = true,
            },
            (msbuildArgs, msbuildPath) => {
                var options = new ReleasePropertyProjectLocator.DependentCommandOptions(
                        nonBinLogArgs,
                        parseResult.HasOption(PublishCommandParser.ConfigurationOption) ? parseResult.GetValue(PublishCommandParser.ConfigurationOption) : null,
                        parseResult.HasOption(PublishCommandParser.FrameworkOption) ? parseResult.GetValue(PublishCommandParser.FrameworkOption) : null
                    );
                var projectLocator = new ReleasePropertyProjectLocator(parseResult, MSBuildPropertyNames.PUBLISH_RELEASE, options);
                var releaseModeProperties = projectLocator.GetCustomDefaultConfigurationValueIfSpecified();
                return new PublishCommand(
                    msbuildArgs: msbuildArgs.CloneWithAdditionalProperties(releaseModeProperties),
                    noRestore: noRestore,
                    msbuildPath: msbuildPath
                );
            },
            [CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, PublishCommandParser.TargetOption, PublishCommandParser.VerbosityOption],
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

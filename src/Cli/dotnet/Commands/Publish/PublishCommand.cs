// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
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
        var definition = (PublishCommandDefinition)parseResult.CommandResult.Command;

        parseResult.HandleDebugSwitch();
        parseResult.ShowHelpOrErrorIfAppropriate();

        string[] args = parseResult.GetValue(definition.SlnOrProjectOrFileArgument) ?? [];

        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);

        CommonOptions.ValidateSelfContainedOptions(parseResult.HasOption(definition.SelfContainedOption),
            parseResult.HasOption(definition.NoSelfContainedOption));

        bool noBuild = parseResult.HasOption(definition.NoBuildOption);

        bool noRestore = noBuild || parseResult.HasOption(definition.NoRestoreOption);

        return CommandFactory.CreateVirtualOrPhysicalCommand(
            definition,
            definition.SlnOrProjectOrFileArgument,
            (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(appFilePath),
                msbuildArgs: msbuildArgs)
            {
                NoBuild = noBuild,
                NoRestore = noRestore,
                NoCache = true,
            },
            (msbuildArgs, msbuildPath) => new PublishCommand(
                msbuildArgs: msbuildArgs,
                noRestore: noRestore,
                msbuildPath: msbuildPath
            ),
            optionsToUseWhenParsingMSBuildFlags:
            [
                CommonOptions.CreatePropertyOption(),
                CommonOptions.CreateRestorePropertyOption(),
                PublishCommandDefinition.CreateTargetOption(),
                CommonOptions.CreateVerbosityOption(),
                CommonOptions.CreateNoLogoOption()
            ],
            parseResult,
            msbuildPath,
            transformer: (msbuildArgs) =>
            {
                var options = new ReleasePropertyProjectLocator.DependentCommandOptions(
                        nonBinLogArgs,
                        parseResult.HasOption(definition.ConfigurationOption) ? parseResult.GetValue(definition.ConfigurationOption) : null,
                        parseResult.HasOption(definition.FrameworkOption) ? parseResult.GetValue(definition.FrameworkOption) : null
                    );
                var projectLocator = new ReleasePropertyProjectLocator(msbuildArgs.GlobalProperties, MSBuildPropertyNames.PUBLISH_RELEASE, options);
                var releaseModeProperties = projectLocator.GetCustomDefaultConfigurationValueIfSpecified();
                return msbuildArgs.CloneWithAdditionalProperties(releaseModeProperties);
            }
        );
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

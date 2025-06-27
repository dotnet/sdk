// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
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
        var parser = Parser.Instance;
        var parseResult = parser.ParseFrom("dotnet publish", args);
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

        var forwardedOptions = parseResult.OptionValuesToBeForwarded(PublishCommandParser.GetCommand());

        bool noBuild = parseResult.HasOption(PublishCommandParser.NoBuildOption);

        bool noRestore = noBuild || parseResult.HasOption(PublishCommandParser.NoRestoreOption);

        if (nonBinLogArgs is [{ } arg] && VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
        {
            var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([.. forwardedOptions, .. binLogArgs, "--property:_IsPublishing=true"], CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, PublishCommandParser.TargetOption);

            msbuildArgs.OtherMSBuildArgs.AddRange(binLogArgs);

            return new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(arg),
                msbuildArgs: msbuildArgs)
            {
                NoBuild = noBuild,
                NoRestore = noRestore,
                NoCache = true,
            };
        }
        else
        {
            ReleasePropertyProjectLocator projectLocator = new(parseResult, MSBuildPropertyNames.PUBLISH_RELEASE,
                new ReleasePropertyProjectLocator.DependentCommandOptions(
                        nonBinLogArgs,
                        parseResult.HasOption(PublishCommandParser.ConfigurationOption) ? parseResult.GetValue(PublishCommandParser.ConfigurationOption) : null,
                        parseResult.HasOption(PublishCommandParser.FrameworkOption) ? parseResult.GetValue(PublishCommandParser.FrameworkOption) : null
                    )
             );
            var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments([
                .. forwardedOptions,
                ..args,
                "--property:_IsPublishing=true",
                ..projectLocator.GetCustomDefaultConfigurationValueIfSpecified()
            ],
            CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, PublishCommandParser.TargetOption);

            return new PublishCommand(
                msbuildArgs,
                noRestore,
                msbuildPath);
        }
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

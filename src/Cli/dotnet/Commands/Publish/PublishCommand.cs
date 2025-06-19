// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Publish;

public class PublishCommand : RestoringCommand
{
    private PublishCommand(
        IEnumerable<string> msbuildArgs,
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

        var msbuildArgs = new List<string>()
        {
            "--property:_IsPublishing=true" // This property will not hold true for MSBuild /t:Publish or in VS.
        };

        string[] args = parseResult.GetValue(PublishCommandParser.SlnOrProjectOrFileArgument) ?? [];

        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);

        CommonOptions.ValidateSelfContainedOptions(parseResult.HasOption(PublishCommandParser.SelfContainedOption),
            parseResult.HasOption(PublishCommandParser.NoSelfContainedOption));

        msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(PublishCommandParser.GetCommand()));

        bool noBuild = parseResult.HasOption(PublishCommandParser.NoBuildOption);

        bool noRestore = noBuild || parseResult.HasOption(PublishCommandParser.NoRestoreOption);

        if (nonBinLogArgs is [{ } arg] && VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
        {
            if (!parseResult.HasOption(PublishCommandParser.ConfigurationOption))
            {
                msbuildArgs.Add("-p:Configuration=Release");
            }

            msbuildArgs.AddRange(binLogArgs);

            return new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(arg),
                msbuildArgs: [.. msbuildArgs])
            {
                NoBuild = noBuild,
                NoRestore = noRestore,
                NoCache = true,
                BuildTarget = "Publish",
            };
        }

        ReleasePropertyProjectLocator projectLocator = new(parseResult, MSBuildPropertyNames.PUBLISH_RELEASE,
            new ReleasePropertyProjectLocator.DependentCommandOptions(
                    nonBinLogArgs,
                    parseResult.HasOption(PublishCommandParser.ConfigurationOption) ? parseResult.GetValue(PublishCommandParser.ConfigurationOption) : null,
                    parseResult.HasOption(PublishCommandParser.FrameworkOption) ? parseResult.GetValue(PublishCommandParser.FrameworkOption) : null
                )
         );
        msbuildArgs.AddRange(projectLocator.GetCustomDefaultConfigurationValueIfSpecified());

        msbuildArgs.AddRange(args ?? []);

        msbuildArgs.Insert(0, "-target:Publish");

        return new PublishCommand(
            msbuildArgs,
            noRestore,
            msbuildPath);
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

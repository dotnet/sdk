// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Build;

public abstract class BuildCommand
{
    public static BuildCommand FromArgs(string[] args, string msbuildPath = null)
    {
        var parser = Parser.Instance;
        var parseResult = parser.ParseFrom("dotnet build", args);
        return FromParseResult(parseResult, msbuildPath);
    }

    public static BuildCommand FromParseResult(ParseResult parseResult, string msbuildPath = null)
    {
        PerformanceLogEventSource.Log.CreateBuildCommandStart();

        parseResult.ShowHelpOrErrorIfAppropriate();

        CommonOptions.ValidateSelfContainedOptions(
            parseResult.GetResult(BuildCommandParser.SelfContainedOption) is not null,
            parseResult.GetResult(BuildCommandParser.NoSelfContainedOption) is not null);

        string[] fileArgument = parseResult.GetValue(BuildCommandParser.SlnOrProjectOrFileArgument) ?? [];

        string[] forwardedOptions = parseResult.OptionValuesToBeForwarded(BuildCommandParser.GetCommand()).ToArray();

        bool noRestore = parseResult.GetResult(BuildCommandParser.NoRestoreOption) is not null;

        bool noIncremental = parseResult.GetResult(BuildCommandParser.NoIncrementalOption) is not null;

        BuildCommand command;

        if (fileArgument is [{ } arg] && VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
        {
            command = new VirtualBuildCommand
            {
                VirtualBuildingCommand = new(
                    entryPointFileFullPath: Path.GetFullPath(arg),
                    msbuildArgs: forwardedOptions,
                    verbosity: parseResult.GetValue(CommonOptions.VerbosityOption),
                    interactive: parseResult.GetValue(CommonOptions.InteractiveMsBuildForwardOption))
                {
                    NoRestore = noRestore,
                    NoCache = true,
                    NoIncremental = noIncremental,
                },
            };
        }
        else
        {
            var msbuildArgs = new List<string>();

            msbuildArgs.Add($"-consoleloggerparameters:Summary");

            if (noIncremental)
            {
                msbuildArgs.Add("-target:Rebuild");
            }

            msbuildArgs.AddRange(forwardedOptions);

            msbuildArgs.AddRange(fileArgument);

            command = new ForwardingBuildCommand(
                msbuildArgs: msbuildArgs,
                noRestore: noRestore,
                msbuildPath: msbuildPath);
        }

        PerformanceLogEventSource.Log.CreateBuildCommandStop();

        return command;
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }

    public abstract int Execute();
}

public sealed class ForwardingBuildCommand(
    IEnumerable<string> msbuildArgs, bool noRestore, string msbuildPath = null) : BuildCommand
{
    public RestoringCommand RestoringCommand { get; } = new RestoringCommand(msbuildArgs, noRestore, msbuildPath);

    public override int Execute() => RestoringCommand.Execute();
}

internal sealed class VirtualBuildCommand : BuildCommand
{
    public required VirtualProjectBuildingCommand VirtualBuildingCommand { get; init; }

    public override int Execute() => VirtualBuildingCommand.Execute();
}

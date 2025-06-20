// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Clean;

public class CleanCommand(IEnumerable<string> msbuildArgs, string? msbuildPath = null) : MSBuildForwardingApp(msbuildArgs, msbuildPath)
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var parser = Parser.Instance;
        var result = parser.ParseFrom("dotnet clean", args);
        return FromParseResult(result, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult result, string? msbuildPath = null)
    {
        var msbuildArgs = new List<string>
        {
            "-verbosity:normal"
        };

        result.ShowHelpOrErrorIfAppropriate();

        var args = result.GetValue(CleanCommandParser.SlnOrProjectOrFileArgument) ?? [];

        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);

        var forwardedArgs = result.OptionValuesToBeForwarded(CleanCommandParser.GetCommand());

        if (nonBinLogArgs is [{ } arg] && VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
        {
            msbuildArgs.AddRange(binLogArgs);
            msbuildArgs.AddRange(forwardedArgs);

            return new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(arg),
                msbuildArgs: [.. msbuildArgs])
            {
                NoBuild = false,
                NoRestore = true,
                NoCache = true,
                BuildTarget = "Clean",
                NoBuildMarkers = true,
            };
        }

        msbuildArgs.AddRange(args);

        msbuildArgs.Add("-target:Clean");

        msbuildArgs.AddRange(forwardedArgs);

        return new CleanCommand(msbuildArgs, msbuildPath);
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

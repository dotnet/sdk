// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Clean;

public class CleanCommand(MSBuildArgs msbuildArgs, string? msbuildPath = null) : MSBuildForwardingApp(msbuildArgs, msbuildPath)
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var parser = Parser.Instance;
        var result = parser.ParseFrom("dotnet clean", args);
        return FromParseResult(result, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult result, string? msbuildPath = null)
    {
        result.ShowHelpOrErrorIfAppropriate();
        return CommandFactory.CreateVirtualOrPhysicalCommand(
            CleanCommandParser.GetCommand(),
            CleanCommandParser.SlnOrProjectOrFileArgument,
            static (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                    entryPointFileFullPath: appFilePath,
                    msbuildArgs: msbuildArgs)
            {
                NoBuild = false,
                NoRestore = true,
                NoCache = true,
                NoBuildMarkers = true,
            },
            static (msbuildArgs, msbuildPath) => new CleanCommand(msbuildArgs, msbuildPath),
            [ CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, CleanCommandParser.TargetOption, CleanCommandParser.VerbosityOption ],
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

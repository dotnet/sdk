// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Restore;

public static class RestoreCommand
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var result = Parser.Parse(["dotnet", "restore", ..args]);
        return FromParseResult(result, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult result, string? msbuildPath = null)
    {
        result.HandleDebugSwitch();
        result.ShowHelpOrErrorIfAppropriate();

        return CommandFactory.CreateVirtualOrPhysicalCommand(
            RestoreCommandParser.GetCommand(),
            RestoreCommandParser.SlnOrProjectOrFileArgument,
            static (msbuildArgs, appFilePath) =>
            {
                return new VirtualProjectBuildingCommand(
                    entryPointFileFullPath: Path.GetFullPath(appFilePath),
                    msbuildArgs: msbuildArgs
                )
                {
                    NoBuild = true,
                    NoCache = true,
                };
            },
            static (msbuildArgs, msbuildPath) =>
            {
                return CreateForwarding(msbuildArgs, msbuildPath);
            },
            [CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, RestoreCommandParser.TargetOption],
            result,
            msbuildPath
        );
    }

    public static MSBuildForwardingApp CreateForwarding(MSBuildArgs msbuildArgs, string? msbuildPath = null)
    {
        var forwardingApp = new MSBuildForwardingApp(msbuildArgs, msbuildPath);
        NuGetSignatureVerificationEnabler.ConditionallyEnable(forwardingApp);
        return forwardingApp;
    }

    public static int Run(string[] args)
    {
        DebugHelper.HandleDebugSwitch(ref args);

        return FromArgs(args).Execute();
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

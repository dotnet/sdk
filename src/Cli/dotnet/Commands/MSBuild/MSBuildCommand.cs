// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

public class MSBuildCommand(
    IEnumerable<string> msbuildArgs,
    string? msbuildPath = null
) : MSBuildForwardingApp(MSBuildArgs.AnalyzeMSBuildArguments([..msbuildArgs], CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, MSBuildCommandParser.TargetOption), msbuildPath, includeLogo: true)
{
    public static MSBuildCommand FromArgs(string[] args, string? msbuildPath = null)
    {
        var result = Parser.Parse(["dotnet", "msbuild", ..args]);
        return FromParseResult(result, msbuildPath);
    }

    public static MSBuildCommand FromParseResult(ParseResult parseResult, string? msbuildPath = null)
    {
        var msbuildArgs = new List<string>();
        msbuildArgs.AddRange(parseResult.GetValue(MSBuildCommandParser.Arguments) ?? []);
        msbuildArgs.AddRange(parseResult.OptionValuesToBeForwarded(MSBuildCommandParser.GetCommand()));

        MSBuildCommand command = new(
            msbuildArgs,
            msbuildPath: msbuildPath);
        return command;
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

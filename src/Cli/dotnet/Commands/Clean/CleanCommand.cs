// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Clean;

public class CleanCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null) : MSBuildForwardingApp(msbuildArgs, msbuildPath)
{
    public static CleanCommand FromArgs(string[] args, string msbuildPath = null)
    {

        var parser = Parser.Instance;
        var result = parser.ParseFrom("dotnet clean", args);
        return FromParseResult(result, msbuildPath);
    }

    public static CleanCommand FromParseResult(ParseResult result, string msbuildPath = null)
    {
        var msbuildArgs = new List<string>
        {
            "-verbosity:normal"
        };

        result.ShowHelpOrErrorIfAppropriate();

        msbuildArgs.AddRange(result.GetValue(CleanCommandParser.SlnOrProjectArgument) ?? []);

        msbuildArgs.Add("-target:Clean");

        msbuildArgs.AddRange(result.OptionValuesToBeForwarded(CleanCommandParser.GetCommand()));

        return new CleanCommand(msbuildArgs, msbuildPath);
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

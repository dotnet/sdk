// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Restore;

public class RestoreCommand : MSBuildForwardingApp
{
    public RestoreCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
        : base(msbuildArgs, msbuildPath)
    {
        NuGetSignatureVerificationEnabler.ConditionallyEnable(this);
    }

    public static RestoreCommand FromArgs(string[] args, string msbuildPath = null)
    {
        var parser = Parser.Instance;
        var result = parser.ParseFrom("dotnet restore", args);
        return FromParseResult(result, msbuildPath);
    }

    public static RestoreCommand FromParseResult(ParseResult result, string msbuildPath = null)
    {
        result.HandleDebugSwitch();

        result.ShowHelpOrErrorIfAppropriate();

        List<string> msbuildArgs = ["-target:Restore"];

        msbuildArgs.AddRange(result.OptionValuesToBeForwarded(RestoreCommandParser.GetCommand()));

        msbuildArgs.AddRange(result.GetValue(RestoreCommandParser.SlnOrProjectArgument) ?? []);

        return new RestoreCommand(msbuildArgs, msbuildPath);
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

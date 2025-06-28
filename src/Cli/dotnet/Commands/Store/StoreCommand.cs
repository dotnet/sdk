// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Store;

public class StoreCommand : MSBuildForwardingApp
{
    private StoreCommand(IEnumerable<string> msbuildArgs, string msbuildPath = null)
        : base(msbuildArgs, msbuildPath)
    {
    }

    public static StoreCommand FromArgs(string[] args, string msbuildPath = null)
    {
        var parser = Parser.Instance;
        var result = parser.ParseFrom("dotnet store", args);
        return FromParseResult(result, msbuildPath);
    }

    public static StoreCommand FromParseResult(ParseResult result, string msbuildPath = null)
    {
        List<string> msbuildArgs = ["--target:ComposeStore"];

        result.ShowHelpOrErrorIfAppropriate();

        if (!result.HasOption(StoreCommandParser.ManifestOption))
        {
            throw new GracefulException(CliCommandStrings.SpecifyManifests);
        }

        msbuildArgs.AddRange(result.OptionValuesToBeForwarded(StoreCommandParser.GetCommand()));

        msbuildArgs.AddRange(result.GetValue(StoreCommandParser.Argument) ?? []);

        return new StoreCommand(msbuildArgs, msbuildPath);
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

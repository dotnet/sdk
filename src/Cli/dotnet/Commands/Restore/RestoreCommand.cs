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
        var parser = Parser.Instance;
        var result = parser.ParseFrom("dotnet restore", args);
        return FromParseResult(result, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult result, string? msbuildPath = null)
    {
        result.HandleDebugSwitch();

        result.ShowHelpOrErrorIfAppropriate();

        string[] args = result.GetValue(RestoreCommandParser.SlnOrProjectOrFileArgument) ?? [];

        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);

        string[] forwardedOptions = result.OptionValuesToBeForwarded(RestoreCommandParser.GetCommand()).ToArray();

        if (nonBinLogArgs is [{ } arg] && VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
        {
            return new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(arg),
                msbuildArgs: [.. forwardedOptions, ..binLogArgs])
            {
                NoCache = true,
                NoBuild = true,
            };
        }

        return CreateForwarding(["-target:Restore", .. forwardedOptions, .. args], msbuildPath);
    }

    public static MSBuildForwardingApp CreateForwarding(IEnumerable<string> msbuildArgs, string? msbuildPath = null)
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

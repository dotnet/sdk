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

        string[] fileArgument = result.GetValue(RestoreCommandParser.SlnOrProjectOrFileArgument) ?? [];

        string[] forwardedOptions = result.OptionValuesToBeForwarded(RestoreCommandParser.GetCommand()).ToArray();

        if (fileArgument is [{ } arg] && VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
        {
            return new VirtualProjectBuildingCommand(
                    entryPointFileFullPath: Path.GetFullPath(arg),
                    msbuildArgs: forwardedOptions,
                    verbosity: result.GetValue(CommonOptions.VerbosityOption),
                    interactive: result.GetValue(CommonOptions.InteractiveMsBuildForwardOption))
            {
                NoCache = true,
                NoBuild = true,
            };
        }

        return CreateForwarding(["-target:Restore", .. forwardedOptions, .. fileArgument], msbuildPath);
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

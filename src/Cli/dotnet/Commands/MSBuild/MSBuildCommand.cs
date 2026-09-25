// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

public class MSBuildCommand(
    IEnumerable<string> msbuildArgs,
    string? msbuildPath,
    CommandServices? services
) : MSBuildForwardingApp(MSBuildArgs.AnalyzeMSBuildArguments(
        [.. msbuildArgs],
        CommonOptions.CreatePropertyOption(),
        CommonOptions.CreateRestorePropertyOption(),
        CommonOptions.CreateMSBuildTargetOption(),
        CommonOptions.CreateVerbosityOption(),
        // We set the no-logo option to false here to ensure that by default the logo is shown for this command.
        // This is different from other commands that default to hiding the logo - but this command is meant to mimic
        // the behavior of calling MSBuild directly, which shows the logo by default.
        CommonOptions.CreateNoLogoOption(false)
    ), msbuildPath, services)
{
    public MSBuildCommand(IEnumerable<string> msbuildArgs, string? msbuildPath = null) : this(msbuildArgs, msbuildPath, services: null)
    {
    }

    public static MSBuildCommand FromArgs(string[] args, string? msbuildPath = null)
        => FromArgs(args, msbuildPath, services: null);

    public static MSBuildCommand FromArgs(string[] args, string? msbuildPath, CommandServices? services)
    {
        var result = Parser.Parse(["dotnet", "msbuild", .. args]);
        return FromParseResult(result, msbuildPath, services);
    }

    public static MSBuildCommand FromParseResult(ParseResult parseResult, string? msbuildPath = null)
        => FromParseResult(parseResult, msbuildPath, services: null);

    public static MSBuildCommand FromParseResult(ParseResult parseResult, string? msbuildPath, CommandServices? services)
    {
        var definition = (MSBuildCommandDefinition)parseResult.CommandResult.Command;

        return new MSBuildCommand(
            msbuildArgs:
            [
                ..parseResult.GetValue(definition.Arguments) ?? [],
                ..parseResult.OptionValuesToBeForwarded(definition)
            ],
            msbuildPath: msbuildPath,
            services: services);
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

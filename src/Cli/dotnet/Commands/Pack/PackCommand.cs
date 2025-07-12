// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Pack;

public class PackCommand(
    MSBuildArgs msbuildArgs,
    bool noRestore,
    string? msbuildPath = null
    ) : RestoringCommand(msbuildArgs, noRestore, msbuildPath: msbuildPath)
{
    public static PackCommand FromArgs(string[] args, string? msbuildPath = null)
    {
        var parser = Parser.Instance;
        var parseResult = parser.ParseFrom("dotnet pack", args);
        return FromParseResult(parseResult, msbuildPath);
    }

    public static PackCommand FromParseResult(ParseResult parseResult, string? msbuildPath = null)
    {
        parseResult.ShowHelpOrErrorIfAppropriate();

        var msbuildArgs = parseResult.OptionValuesToBeForwarded(PackCommandParser.GetCommand()).Concat(parseResult.GetValue(PackCommandParser.SlnOrProjectArgument) ?? []);

        ReleasePropertyProjectLocator projectLocator = new(parseResult, MSBuildPropertyNames.PACK_RELEASE,
            new ReleasePropertyProjectLocator.DependentCommandOptions(
                    parseResult.GetValue(PackCommandParser.SlnOrProjectArgument),
                    parseResult.HasOption(PackCommandParser.ConfigurationOption) ? parseResult.GetValue(PackCommandParser.ConfigurationOption) : null
                )
        );

        bool noRestore = parseResult.HasOption(PackCommandParser.NoRestoreOption) || parseResult.HasOption(PackCommandParser.NoBuildOption);
        var parsedMSBuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(
            msbuildArgs,
            CommonOptions.PropertiesOption,
            CommonOptions.RestorePropertiesOption,
            PackCommandParser.TargetOption,
            PackCommandParser.VerbosityOption);
        return new PackCommand(
            parsedMSBuildArgs.CloneWithAdditionalProperties(projectLocator.GetCustomDefaultConfigurationValueIfSpecified()),
            noRestore,
            msbuildPath);
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }
}

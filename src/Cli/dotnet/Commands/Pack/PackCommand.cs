// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Commands;
using NuGet.Packaging.Core;

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
            PackCommandParser.TargetOption);

        return new PackCommand(
            parsedMSBuildArgs.CloneWithAdditionalProperties(projectLocator.GetCustomDefaultConfigurationValueIfSpecified()),
            noRestore,
            msbuildPath);
    }

    public static int RunPackCommand(ParseResult parseResult)
    {
        var args = parseResult.GetValue(PackCommandParser.SlnOrProjectArgument)?.ToList() ?? new List<string>();
        var nuspecPath = args[0];

        var packArgs = new PackArgs()
        {
            Path = nuspecPath,
            BasePath = Path.GetDirectoryName(nuspecPath),
            Logger = new NuGetConsoleLogger(),
            Exclude = new List<string>()
        };

        var properties = parseResult.GetValue(PackCommandParser.PropertiesOption);
        if (properties != null)
        {
            foreach (var prop in properties)
            {
                var split = prop.Split('=', 2);
                if (split.Length == 2)
                    packArgs.Properties[split[0]] = split[1];
            }
        }a

        var version = parseResult.GetValue(PackCommandParser.VersionOption);
        if (!string.IsNullOrEmpty(version))
            packArgs.Version = version;

        var packCommandRunner = new PackCommandRunner(packArgs, null);
        if (!packCommandRunner.RunPackageBuild())
            return 1;
        return 0;
    }


    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();
        parseResult.ShowHelpOrErrorIfAppropriate();

        var args = parseResult.GetValue(PackCommandParser.SlnOrProjectArgument)?.ToList() ?? new List<string>();

        if (args.Count > 0 && Path.GetExtension(args[0]).Equals(".nuspec", StringComparison.OrdinalIgnoreCase))
        {
            return RunPackCommand(parseResult);
        }

        // Fallback to MSBuild-based packing
        return FromParseResult(parseResult).Execute();
    }
}

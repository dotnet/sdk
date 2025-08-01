// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Configuration;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging;
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
        var parseResult = Parser.Parse(["dotnet", "pack", ..args]);
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
            PackCommandParser.TargetOption,
            PackCommandParser.VerbosityOption);
        return new PackCommand(
            parsedMSBuildArgs.CloneWithAdditionalProperties(projectLocator.GetCustomDefaultConfigurationValueIfSpecified()),
            noRestore,
            msbuildPath);
    }

    private static LogLevel MappingVerbosityToNugetLogLevel(VerbosityOptions? verbosity)
    {
        return verbosity switch
        {
            VerbosityOptions.diagnostic or VerbosityOptions.diag => LogLevel.Debug,
            VerbosityOptions.minimal or VerbosityOptions.m => LogLevel.Minimal,
            VerbosityOptions.normal or VerbosityOptions.n => LogLevel.Information,
            VerbosityOptions.detailed or VerbosityOptions.d => LogLevel.Verbose,
            _ => LogLevel.Minimal
        };
    }

    public static int RunPackCommand(ParseResult parseResult)
    {
        var args = parseResult.GetValue(PackCommandParser.SlnOrProjectArgument)?.ToList() ?? new List<string>();

        if (args.Count != 1)
        {
            Console.Error.WriteLine(CliStrings.PackCmd_OneNuspecAllowed); 
            return 1;
        }

        var nuspecPath = args[0];

        var packArgs = new PackArgs()
        { 
            Logger = new NuGetConsoleLogger(),
            Exclude = new List<string>(),
            OutputDirectory = parseResult.GetValue(PackCommandParser.OutputOption),
            LogLevel = MappingVerbosityToNugetLogLevel(parseResult.GetValue(BuildCommandParser.VerbosityOption)),
            Arguments = [nuspecPath]
        };

        packArgs.Path = PackCommandRunner.GetInputFile(packArgs);
        packArgs.BasePath = Path.GetDirectoryName(packArgs.Path);
        PackCommandRunner.SetupCurrentDirectory(packArgs);

        var globalProperties = parseResult.GetResult("--property") is OptionResult propResult ? propResult.GetValueOrDefault<ReadOnlyDictionary<string, string>?>() : null;
        if (globalProperties != null)
            packArgs.Properties.AddRange(globalProperties);

        var version = parseResult.GetValue(CommonOptions.VersionOption);
        if (version != null)
            packArgs.Version = version.ToNormalizedString();

        var configuration = parseResult.GetValue(PackCommandParser.ConfigurationOption) ?? "Debug";
        packArgs.Properties["configuration"] = configuration;

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

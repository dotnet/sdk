// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Commands.Pack;

public class PackCommand(
    MSBuildArgs msbuildArgs,
    bool noRestore,
    string? msbuildPath = null
    ) : RestoringCommand(msbuildArgs, noRestore, msbuildPath: msbuildPath)
{
    public static CommandBase FromArgs(string[] args, string? msbuildPath = null)
    {
        var parseResult = Parser.Parse(["dotnet", "pack", ..args]);
        return FromParseResult(parseResult, msbuildPath);
    }

    public static CommandBase FromParseResult(ParseResult parseResult, string? msbuildPath = null)
    {
        var definition = (PackCommandDefinition)parseResult.CommandResult.Command;
        var args = parseResult.GetValue(definition.SlnOrProjectOrFileArgument) ?? [];

        LoggerUtility.SeparateBinLogArguments(args, out var binLogArgs, out var nonBinLogArgs);

        bool noBuild = parseResult.HasOption(definition.NoBuildOption);

        bool noRestore = noBuild || parseResult.HasOption(definition.NoRestoreOption);

        return CommandFactory.CreateVirtualOrPhysicalCommand(
            definition,
            definition.SlnOrProjectOrFileArgument,
            (msbuildArgs, appFilePath) => new VirtualProjectBuildingCommand(
                entryPointFileFullPath: Path.GetFullPath(appFilePath),
                msbuildArgs: msbuildArgs)
            {
                NoBuild = noBuild,
                NoRestore = noRestore,
                NoCache = true,
            },
            (msbuildArgs, msbuildPath) => new PackCommand(
                msbuildArgs,
                noRestore,
                msbuildPath),
            optionsToUseWhenParsingMSBuildFlags:
            [
                CommonOptions.CreatePropertyOption(),
                CommonOptions.CreateRestorePropertyOption(),
                PackCommandDefinition.CreateTargetOption(),
                CommonOptions.CreateVerbosityOption(),
                CommonOptions.CreateNoLogoOption()
            ],
            parseResult,
            msbuildPath,
            transformer: (msbuildArgs) =>
            {
                ReleasePropertyProjectLocator projectLocator = new(msbuildArgs.GlobalProperties, MSBuildPropertyNames.PACK_RELEASE,
                    new ReleasePropertyProjectLocator.DependentCommandOptions(
                            nonBinLogArgs,
                            parseResult.HasOption(definition.ConfigurationOption) ? parseResult.GetValue(definition.ConfigurationOption) : null
                        )
                );
                return msbuildArgs.CloneWithAdditionalProperties(projectLocator.GetCustomDefaultConfigurationValueIfSpecified());
            });
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
        var definition = (PackCommandDefinition)parseResult.CommandResult.Command;

        var args = parseResult.GetValue(definition.SlnOrProjectOrFileArgument)?.ToList() ?? new List<string>();

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
            OutputDirectory = parseResult.GetValue(definition.OutputOption),
            LogLevel = MappingVerbosityToNugetLogLevel(parseResult.GetValue(definition.VerbosityOption)),
            Arguments = [nuspecPath]
        };

        packArgs.Path = PackCommandRunner.GetInputFile(packArgs);
        packArgs.BasePath = Path.GetDirectoryName(packArgs.Path);
        PackCommandRunner.SetupCurrentDirectory(packArgs);

        var globalProperties = parseResult.GetResult("--property") is OptionResult propResult ? propResult.GetValueOrDefault<ReadOnlyDictionary<string, string>?>() : null;
        if (globalProperties != null)
            packArgs.Properties.AddRange(globalProperties);

        var version = parseResult.GetValue(definition.VersionOption);
        if (version != null)
            packArgs.Version = version.ToNormalizedString();

        var configuration = parseResult.GetValue(definition.ConfigurationOption) ?? "Debug";
        packArgs.Properties["configuration"] = configuration;

        var packCommandRunner = new PackCommandRunner(packArgs, null);
        if (!packCommandRunner.RunPackageBuild())
            return 1;
        return 0;
    }

    public static int Run(ParseResult parseResult)
    {
        var definition = (PackCommandDefinition)parseResult.CommandResult.Command;

        parseResult.HandleDebugSwitch();
        parseResult.ShowHelpOrErrorIfAppropriate();

        var args = parseResult.GetValue(definition.SlnOrProjectOrFileArgument)?.ToList() ?? new List<string>();

        if (args.Count > 0 && Path.GetExtension(args[0]).Equals(".nuspec", StringComparison.OrdinalIgnoreCase))
        {
            return RunPackCommand(parseResult);
        }

        // Fallback to MSBuild-based packing
        return FromParseResult(parseResult).Execute();
    }
}

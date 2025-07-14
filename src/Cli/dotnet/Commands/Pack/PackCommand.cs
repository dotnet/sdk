// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Commands;

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
        /*parseResult.ShowHelpOrErrorIfAppropriate();
        var args = parseResult.GetValue(PackCommandParser.SlnOrProjectArgument)?.ToList() ?? new List<string>();

        // If --nuspec is specified, pack the nuspec file directly using NuGet APIs
        if (parseResult.GetValue(PackCommandParser.Nuspec) == true)
        {
            var nuspecPath = args.FirstOrDefault();
            if (string.IsNullOrEmpty(nuspecPath) || !nuspecPath.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("You must specify a .nuspec file as the positional argument when using --nuspec.");
            if (!File.Exists(nuspecPath))
                throw new FileNotFoundException($"The specified .nuspec file '{nuspecPath}' does not exist.");

            var packArgs = new PackArgs()
            {
                Path = nuspecPath,
                BasePath = Directory.GetCurrentDirectory(),
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
            }

            var version = parseResult.GetValue(PackCommandParser.VersionOption);
            if (!string.IsNullOrEmpty(version))
                packArgs.Version = version;

            var packCommandRunner = new PackCommandRunner(packArgs, null);
            if (!packCommandRunner.RunPackageBuild())
                throw new InvalidOperationException("Failed to run pack command with nuspec option.");

            // Return a dummy PackCommand to satisfy the method signature
            return new PackCommand(MSBuildArgs.ForHelp, true, msbuildPath);
        }*/
        

        // Otherwise, fall back to existing MSBuild-based logic for projects/solutions
        var msbuildArgs = parseResult.OptionValuesToBeForwarded(PackCommandParser.GetCommand())
            .Concat(parseResult.GetValue(PackCommandParser.SlnOrProjectArgument) ?? []);

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
            CommonOptions.RestorePropertiesOption
            //PackCommandParser.TargetOption
            );

        return new PackCommand(
            parsedMSBuildArgs.CloneWithAdditionalProperties(projectLocator.GetCustomDefaultConfigurationValueIfSpecified()),
            noRestore,
            msbuildPath);
    }

    public static int RunPackCommand(ParseResult parseResult)
    {
        parseResult.ShowHelpOrErrorIfAppropriate();
        var args = parseResult.GetValue(PackCommandParser.SlnOrProjectArgument)?.ToList() ?? new List<string>();

        // If --nuspec is specified, pack the nuspec file directly using NuGet APIs
        if (parseResult.GetValue(PackCommandParser.Nuspec) == true)
        {
            var nuspecPath = args.FirstOrDefault();
            if (string.IsNullOrEmpty(nuspecPath) || !nuspecPath.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("You must specify a .nuspec file as the positional argument when using --nuspec.");
            if (!File.Exists(nuspecPath))
                throw new FileNotFoundException($"The specified .nuspec file '{nuspecPath}' does not exist.");

            var packArgs = new PackArgs()
            {
                Path = nuspecPath,
                BasePath = Path.GetDirectoryName(nuspecPath),
                Logger = new NuGetConsoleLogger(),
                Exclude = new List<string>()
            };

            var properties = parseResult.GetValue(PackCommandParser.PropertiesOption);
            string? idOverride = null;
            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    var split = prop.Split('=', 2);
                    if (split.Length == 2)
                    {
                        if (split[0].Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                            split[0].Equals("PackageId", StringComparison.OrdinalIgnoreCase))
                            {
                            idOverride = split[1]; 
                        }
                    }
                        packArgs.Properties[split[0]] = split[1];
                }
            }

            var version = parseResult.GetValue(PackCommandParser.VersionOption);
            if (!string.IsNullOrEmpty(version))
                packArgs.Version = version;

            var packCommandRunner = new PackCommandRunner(packArgs, null);
            if (!packCommandRunner.RunPackageBuild())
                // throw new InvalidOperationException("Failed to run pack command with nuspec option.");
                return 1;
        }
        return 0;
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();
        if (parseResult.GetValue(PackCommandParser.Nuspec) == true)
            return RunPackCommand(parseResult);
        return FromParseResult(parseResult).Execute();
    }
}

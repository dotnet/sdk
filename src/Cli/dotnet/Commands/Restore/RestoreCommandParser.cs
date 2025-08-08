// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal static class RestoreCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-restore";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<IEnumerable<string>> SourceOption = new ForwardedOption<IEnumerable<string>>("--source", "-s")
    {
        Description = CliCommandStrings.CmdSourceOptionDescription,
        HelpName = CliCommandStrings.CmdSourceOption
    }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}")
    .AllowSingleArgPerToken();

    public static readonly Option<string[]> TargetOption = CommonOptions.RequiredMSBuildTargetOption("Restore");
    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption(Utils.VerbosityOptions.minimal);

    private static IEnumerable<Option> FullRestoreOptions() =>
        ImplicitRestoreOptions(true, true, true, true).Concat(
            [
                VerbosityOption,
                CommonOptions.InteractiveMsBuildForwardOption,
                CommonOptions.ArtifactsPathOption,
                new ForwardedOption<bool>("--use-lock-file")
                {
                    Description = CliCommandStrings.CmdUseLockFileOptionDescription,
                    Arity = ArgumentArity.Zero
                }.ForwardAs("-property:RestorePackagesWithLockFile=true"),
                new ForwardedOption<bool>("--locked-mode")
                {
                    Description = CliCommandStrings.CmdLockedModeOptionDescription,
                    Arity = ArgumentArity.Zero
                }.ForwardAs("-property:RestoreLockedMode=true"),
                new ForwardedOption<string>("--lock-file-path")
                {
                    Description = CliCommandStrings.CmdLockFilePathOptionDescription,
                    HelpName = CliCommandStrings.CmdLockFilePathOption
                }.ForwardAsSingle(o => $"-property:NuGetLockFilePath={o}"),
                new ForwardedOption<bool>("--force-evaluate")
                {
                    Description = CliCommandStrings.CmdReevaluateOptionDescription,
                    Arity = ArgumentArity.Zero
                }.ForwardAs("-property:RestoreForceEvaluate=true"),
                TargetOption
            ]);

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new DocumentedCommand("restore", DocsLink, CliCommandStrings.RestoreAppFullName);

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(CommonOptions.BinaryLoggerOption);

        command.Options.AddRange(FullRestoreOptions());

        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.SetAction(RestoreCommand.Run);

        return command;
    }

    public static void AddImplicitRestoreOptions(Command command, bool showHelp = false, bool useShortOptions = false, bool includeRuntimeOption = true, bool includeNoDependenciesOption = true)
    {
        foreach (var option in ImplicitRestoreOptions(showHelp, useShortOptions, includeRuntimeOption, includeNoDependenciesOption))
        {
            command.Options.Add(option);
        }
    }
    private static string GetOsFromRid(string rid) => rid.Substring(0, rid.LastIndexOf("-", StringComparison.InvariantCulture));
    private static string GetArchFromRid(string rid) => rid.Substring(rid.LastIndexOf("-", StringComparison.InvariantCulture) + 1, rid.Length - rid.LastIndexOf("-", StringComparison.InvariantCulture) - 1);
    public static string RestoreRuntimeArgFunc(IEnumerable<string> rids)
    {
        List<string> convertedRids = [];
        foreach (string rid in rids)
        {
            if (GetArchFromRid(rid.ToString()) == "amd64")
            {
                convertedRids.Add($"{GetOsFromRid(rid.ToString())}-x64");
            }
            else
            {
                convertedRids.Add($"{rid}");
            }
        }
        return $"-property:RuntimeIdentifiers={string.Join("%3B", convertedRids)}";
    }

    private static IEnumerable<Option> ImplicitRestoreOptions(bool showHelp, bool useShortOptions, bool includeRuntimeOption, bool includeNoDependenciesOption)
    {
        if (showHelp && useShortOptions)
        {
            yield return SourceOption;
        }
        else
        {
            Option<IEnumerable<string>> sourceOption = new ForwardedOption<IEnumerable<string>>("--source")
            {
                Description = showHelp ? CliCommandStrings.CmdSourceOptionDescription : string.Empty,
                HelpName = CliCommandStrings.CmdSourceOption,
                Hidden = !showHelp
            }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}") // '%3B' corresponds to ';'
            .AllowSingleArgPerToken();

            if (useShortOptions)
            {
                sourceOption.Aliases.Add("-s");
            }

            yield return sourceOption;
        }

        yield return new ForwardedOption<string>("--packages")
        {
            Description = showHelp ? CliCommandStrings.CmdPackagesOptionDescription : string.Empty,
            HelpName = CliCommandStrings.CmdPackagesOption,
            Hidden = !showHelp
        }.ForwardAsSingle(o => $"-property:RestorePackagesPath={CommandDirectoryContext.GetFullPath(o)}");

        yield return CommonOptions.CurrentRuntimeOption(CliCommandStrings.CmdCurrentRuntimeOptionDescription);

        yield return new ForwardedOption<bool>("--disable-parallel")
        {
            Description = showHelp ? CliCommandStrings.CmdDisableParallelOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreDisableParallel=true");

        yield return new ForwardedOption<string>("--configfile")
        {
            Description = showHelp ? CliCommandStrings.CmdConfigFileOptionDescription : string.Empty,
            HelpName = CliCommandStrings.CmdConfigFileOption,
            Hidden = !showHelp
        }.ForwardAsSingle(o => $"-property:RestoreConfigFile={CommandDirectoryContext.GetFullPath(o)}");

        yield return new ForwardedOption<bool>("--no-cache")
        {
            Description = string.Empty,
            Hidden = true,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreNoCache=true");

        yield return new ForwardedOption<bool>("--no-http-cache")
        {
            Description = showHelp ? CliCommandStrings.CmdNoHttpCacheOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreNoHttpCache=true");

        yield return new ForwardedOption<bool>("--ignore-failed-sources")
        {
            Description = showHelp ? CliCommandStrings.CmdIgnoreFailedSourcesOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreIgnoreFailedSources=true");

        ForwardedOption<bool> forceOption = new ForwardedOption<bool>("--force")
        {
            Description = CliCommandStrings.CmdForceRestoreOptionDescription,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreForce=true");
        if (useShortOptions)
        {
            forceOption.Aliases.Add("-f");
        }
        yield return forceOption;

        yield return CommonOptions.PropertiesOption;
        yield return CommonOptions.RestorePropertiesOption;

        if (includeRuntimeOption)
        {
            Option<IEnumerable<string>> runtimeOption = new DynamicForwardedOption<IEnumerable<string>>("--runtime")
            {
                Description = CliCommandStrings.CmdRuntimeOptionDescription,
                HelpName = CliCommandStrings.CmdRuntimeOption,
                Hidden = !showHelp,
            }.ForwardAsSingle(RestoreRuntimeArgFunc)
             .AllowSingleArgPerToken()
             .AddCompletions(CliCompletion.RunTimesFromProjectFile);

            if (useShortOptions)
            {
                runtimeOption.Aliases.Add("-r");
            }

            yield return runtimeOption;
        }

        if (includeNoDependenciesOption)
        {
            yield return new ForwardedOption<bool>("--no-dependencies")
            {
                Description = CliCommandStrings.CmdNoDependenciesOptionDescription,
                Arity = ArgumentArity.Zero,
                Hidden = !showHelp
            }.ForwardAs("-property:RestoreRecursive=false");
        }
    }
}

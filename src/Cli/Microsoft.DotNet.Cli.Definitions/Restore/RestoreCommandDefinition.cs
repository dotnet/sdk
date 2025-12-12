// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal static class RestoreCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-restore";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliDefinitionResources.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliDefinitionResources.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<IEnumerable<string>> SourceOption = new Option<IEnumerable<string>>("--source", "-s")
    {
        Description = CliDefinitionResources.CmdSourceOptionDescription,
        HelpName = CliDefinitionResources.CmdSourceOption
    }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}")
    .AllowSingleArgPerToken();

    public static readonly Option<string[]> TargetOption = CommonOptions.RequiredMSBuildTargetOption("Restore");
    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption(Utils.VerbosityOptions.minimal);
    public static readonly Option<bool> NoLogoOption = CommonOptions.NoLogoOption();

    private static IEnumerable<Option> FullRestoreOptions() => [
        ..ImplicitRestoreOptions(true, true, true, true),
        VerbosityOption,
        CommonOptions.InteractiveMsBuildForwardOption,
        CommonOptions.ArtifactsPathOption,
        new Option<bool>("--use-lock-file")
        {
            Description = CliDefinitionResources.CmdUseLockFileOptionDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestorePackagesWithLockFile=true"),
        new Option<bool>("--locked-mode")
        {
            Description = CliDefinitionResources.CmdLockedModeOptionDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreLockedMode=true"),
        new Option<string>("--lock-file-path")
        {
            Description = CliDefinitionResources.CmdLockFilePathOptionDescription,
            HelpName = CliDefinitionResources.CmdLockFilePathOption
        }.ForwardAsSingle(o => $"-property:NuGetLockFilePath={o}"),
        new Option<bool>("--force-evaluate")
        {
            Description = CliDefinitionResources.CmdReevaluateOptionDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreForceEvaluate=true"),
        TargetOption,
        NoLogoOption
    ];

    public static Command Create()
    {
        var command = new Command("restore", CliDefinitionResources.RestoreAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        command.Options.Add(CommonOptions.DisableBuildServersOption);

        command.Options.AddRange(FullRestoreOptions());

        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.GetPropertyOption);
        command.Options.Add(CommonOptions.GetItemOption);
        command.Options.Add(CommonOptions.GetTargetResultOption);
        command.Options.Add(CommonOptions.GetResultOutputFileOption);

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
            Option<IEnumerable<string>> sourceOption = new Option<IEnumerable<string>>("--source")
            {
                Description = showHelp ? CliDefinitionResources.CmdSourceOptionDescription : string.Empty,
                HelpName = CliDefinitionResources.CmdSourceOption,
                Hidden = !showHelp
            }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}") // '%3B' corresponds to ';'
            .AllowSingleArgPerToken();

            if (useShortOptions)
            {
                sourceOption.Aliases.Add("-s");
            }

            yield return sourceOption;
        }

        yield return new Option<string>("--packages")
        {
            Description = showHelp ? CliDefinitionResources.CmdPackagesOptionDescription : string.Empty,
            HelpName = CliDefinitionResources.CmdPackagesOption,
            Hidden = !showHelp
        }.ForwardAsSingle(o => $"-property:RestorePackagesPath={CommandDirectoryContext.GetFullPath(o)}");

        yield return CommonOptions.CurrentRuntimeOption(CliDefinitionResources.CmdCurrentRuntimeOptionDescription);

        yield return new Option<bool>("--disable-parallel")
        {
            Description = showHelp ? CliDefinitionResources.CmdDisableParallelOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreDisableParallel=true");

        yield return new Option<string>("--configfile")
        {
            Description = showHelp ? CliDefinitionResources.CmdConfigFileOptionDescription : string.Empty,
            HelpName = CliDefinitionResources.CmdConfigFileOption,
            Hidden = !showHelp
        }.ForwardAsSingle(o => $"-property:RestoreConfigFile={CommandDirectoryContext.GetFullPath(o)}");

        yield return new Option<bool>("--no-cache")
        {
            Description = string.Empty,
            Hidden = true,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreNoCache=true");

        yield return new Option<bool>("--no-http-cache")
        {
            Description = showHelp ? CliDefinitionResources.CmdNoHttpCacheOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreNoHttpCache=true");

        yield return new Option<bool>("--ignore-failed-sources")
        {
            Description = showHelp ? CliDefinitionResources.CmdIgnoreFailedSourcesOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreIgnoreFailedSources=true");

        Option<bool> forceOption = new Option<bool>("--force")
        {
            Description = CliDefinitionResources.CmdForceRestoreOptionDescription,
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
            Option<IEnumerable<string>> runtimeOption = new Option<IEnumerable<string>>("--runtime")
            {
                Description = CliDefinitionResources.CmdRuntimeOptionDescription,
                HelpName = CliDefinitionResources.CmdRuntimeOption,
                Hidden = !showHelp,
                IsDynamic = true
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
            yield return new Option<bool>("--no-dependencies")
            {
                Description = CliDefinitionResources.CmdNoDependenciesOptionDescription,
                Arity = ArgumentArity.Zero,
                Hidden = !showHelp
            }.ForwardAs("-property:RestoreRecursive=false");
        }
    }
}

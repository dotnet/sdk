// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal sealed class ImplicitRestoreOptions
{
    public readonly Option<IEnumerable<string>> SourceOption;
    public readonly Option<string> PackagesOption;
    public readonly Option<bool> CurrentRuntimeOption;
    public readonly Option<bool> DisableParallelOption;
    public readonly Option<string> ConfigFileOption;
    public readonly Option<bool> NoCacheOption;
    public readonly Option<bool> NoHttpCacheOption;
    public readonly Option<bool> IgnoreFailedSourcesOption;
    public readonly Option<bool> ForceOption;
    public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption;
    public readonly Option<ReadOnlyDictionary<string, string>?> RestorePropertiesOption;

    public ImplicitRestoreOptions(bool showHelp, bool useShortOptions)
    {
        SourceOption = CreateSourceOption(showHelp, useShortOptions);
        
        PackagesOption = new Option<string>("--packages")
        {
            Description = showHelp ? CommandDefinitionStrings.CmdPackagesOptionDescription : string.Empty,
            HelpName = CommandDefinitionStrings.CmdPackagesOption,
            Hidden = !showHelp
        }.ForwardAsSingle(o => $"-property:RestorePackagesPath={CommandDirectoryContext.GetFullPath(o)}");

        CurrentRuntimeOption = CommonOptions.CreateUseCurrentRuntimeOption(CommandDefinitionStrings.CmdCurrentRuntimeOptionDescription);

        DisableParallelOption = new Option<bool>("--disable-parallel")
        {
            Description = showHelp ? CommandDefinitionStrings.CmdDisableParallelOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreDisableParallel=true");

        ConfigFileOption = new Option<string>("--configfile")
        {
            Description = showHelp ? CommandDefinitionStrings.CmdConfigFileOptionDescription : string.Empty,
            HelpName = CommandDefinitionStrings.CmdConfigFileOption,
            Hidden = !showHelp
        }.ForwardAsSingle(o => $"-property:RestoreConfigFile={CommandDirectoryContext.GetFullPath(o)}");

        NoCacheOption = new Option<bool>("--no-cache")
        {
            Description = string.Empty,
            Hidden = true,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreNoCache=true");

        NoHttpCacheOption = new Option<bool>("--no-http-cache")
        {
            Description = showHelp ? CommandDefinitionStrings.CmdNoHttpCacheOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreNoHttpCache=true");

        IgnoreFailedSourcesOption = new Option<bool>("--ignore-failed-sources")
        {
            Description = showHelp ? CommandDefinitionStrings.CmdIgnoreFailedSourcesOptionDescription : string.Empty,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreIgnoreFailedSources=true");

        ForceOption = new Option<bool>("--force")
        {
            Description = CommandDefinitionStrings.CmdForceRestoreOptionDescription,
            Hidden = !showHelp,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreForce=true");
        if (useShortOptions)
        {
            ForceOption.Aliases.Add("-f");
        }

        PropertiesOption = CommonOptions.CreatePropertyOption();
        RestorePropertiesOption = CommonOptions.CreateRestorePropertyOption();
    }

    private static Option<IEnumerable<string>> CreateSourceOption(bool showHelp, bool useShortOptions)
    {
        var option = new Option<IEnumerable<string>>("--source")
        {
            Description = showHelp ? CommandDefinitionStrings.CmdSourceOptionDescription : string.Empty,
            HelpName = CommandDefinitionStrings.CmdSourceOption,
            Hidden = !showHelp
        }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}")
         .AllowSingleArgPerToken();

        if (useShortOptions)
        {
            option.Aliases.Add("-s");
        }

        return option;
    }

    public void AddTo(IList<Option> options)
    {
        options.Add(SourceOption);
        options.Add(PackagesOption);
        options.Add(CurrentRuntimeOption);
        options.Add(DisableParallelOption);
        options.Add(ConfigFileOption);
        options.Add(NoCacheOption);
        options.Add(NoHttpCacheOption);
        options.Add(IgnoreFailedSourcesOption);
        options.Add(ForceOption);
        options.Add(PropertiesOption);
        options.Add(RestorePropertiesOption);
    }
}

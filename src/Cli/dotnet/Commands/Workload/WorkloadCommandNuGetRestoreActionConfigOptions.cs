// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli;

internal static class WorkloadCommandNuGetRestoreActionConfigOptions
{
    public static CliOption<bool> DisableParallelOption = new ForwardedOption<bool>("--disable-parallel")
    {
        Description = LocalizableStrings.CmdDisableParallelOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static CliOption<bool> NoCacheOption = new ForwardedOption<bool>("--no-cache")
    {
        Description = LocalizableStrings.CmdNoCacheOptionDescription,
        Hidden = true,
        Arity = ArgumentArity.Zero
    };

    public static CliOption<bool> NoHttpCacheOption = new ForwardedOption<bool>("--no-http-cache")
    {
        Description = LocalizableStrings.CmdNoCacheOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static CliOption<bool> IgnoreFailedSourcesOption = new ForwardedOption<bool>("--ignore-failed-sources")
    {
        Description = LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static CliOption<bool> InteractiveRestoreOption = CommonOptions.InteractiveOption();

    public static CliOption<bool> HiddenDisableParallelOption = new ForwardedOption<bool>("--disable-parallel")
    {
        Description = LocalizableStrings.CmdDisableParallelOptionDescription,
        Arity = ArgumentArity.Zero
    }.Hide();

    public static CliOption<bool> HiddenNoCacheOption = new ForwardedOption<bool>("--no-cache")
    {
        Description = LocalizableStrings.CmdNoCacheOptionDescription,
        Arity = ArgumentArity.Zero
    }.Hide();

    public static CliOption<bool> HiddenNoHttpCacheOption = new ForwardedOption<bool>("--no-http-cache")
    {
        Description = LocalizableStrings.CmdNoCacheOptionDescription,
        Arity = ArgumentArity.Zero
    }.Hide();

    public static CliOption<bool> HiddenIgnoreFailedSourcesOption = new ForwardedOption<bool>("--ignore-failed-sources")
    {
        Description = LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription,
        Arity = ArgumentArity.Zero
    }.Hide();

    public static CliOption<bool> HiddenInteractiveRestoreOption = new ForwardedOption<bool>("--interactive")
    {
        Description = CommonLocalizableStrings.CommandInteractiveOptionDescription,
    }.Hide();

    public static RestoreActionConfig ToRestoreActionConfig(this ParseResult parseResult)
    {
        return new RestoreActionConfig(DisableParallel: parseResult.GetValue(DisableParallelOption),
            NoCache: parseResult.GetValue(NoCacheOption) || parseResult.GetValue(NoHttpCacheOption),
            IgnoreFailedSources: parseResult.GetValue(IgnoreFailedSourcesOption),
            Interactive: parseResult.GetValue(InteractiveRestoreOption));
    }

    public static void AddWorkloadCommandNuGetRestoreActionConfigOptions(this CliCommand command, bool Hide = false)
    {
        command.Options.Add(Hide ? HiddenDisableParallelOption : DisableParallelOption);
        command.Options.Add(Hide ? HiddenIgnoreFailedSourcesOption : IgnoreFailedSourcesOption);
        command.Options.Add(Hide ? HiddenNoCacheOption : NoCacheOption);
        command.Options.Add(Hide ? HiddenNoHttpCacheOption : NoHttpCacheOption);
        command.Options.Add(Hide ? HiddenInteractiveRestoreOption : InteractiveRestoreOption);
    }
}

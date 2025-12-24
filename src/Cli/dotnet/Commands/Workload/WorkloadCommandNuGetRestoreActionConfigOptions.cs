// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal sealed class WorkloadCommandNuGetRestoreActionConfigOptions(bool hidden = false)
{
    public readonly Option<bool> DisableParallelOption = new("--disable-parallel")
    {
        Description = CliCommandStrings.CmdDisableParallelOptionDescription,
        Arity = ArgumentArity.Zero,
        Hidden = hidden
    };

    public readonly Option<bool> NoCacheOption = new("--no-cache")
    {
        Description = CliCommandStrings.CmdNoCacheOptionDescription,
        Hidden = hidden,
        Arity = ArgumentArity.Zero,
    };

    public readonly Option<bool> NoHttpCacheOption = new("--no-http-cache")
    {
        Description = CliCommandStrings.CmdNoCacheOptionDescription,
        Arity = ArgumentArity.Zero,
        Hidden = hidden
    };

    public readonly Option<bool> IgnoreFailedSourcesOption = new("--ignore-failed-sources")
    {
        Description = CliCommandStrings.CmdIgnoreFailedSourcesOptionDescription,
        Arity = ArgumentArity.Zero,
        Hidden = hidden
    };

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption(hidden: hidden);

    public void AddTo(IList<Option> options)
    {
        options.Add(DisableParallelOption);
        options.Add(NoCacheOption);
        options.Add(NoHttpCacheOption);
        options.Add(IgnoreFailedSourcesOption);
        options.Add(InteractiveOption);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands;

internal sealed class NuGetRestoreOptions(bool hidden = false, bool forward = false)
{
    public readonly Option<bool> DisableParallelOption = ForwardWhen<bool>(new("--disable-parallel")
    {
        Description = CommandDefinitionStrings.CmdDisableParallelOptionDescription,
        Arity = ArgumentArity.Zero,
        Hidden = hidden
    }, forward);

    public readonly Option<bool> NoCacheOption = ForwardWhen<bool>(new("--no-cache")
    {
        Description = CommandDefinitionStrings.CmdNoCacheOptionDescription,
        Hidden = true,
        Arity = ArgumentArity.Zero,
    }, forward);

    public readonly Option<bool> NoHttpCacheOption = ForwardWhen<bool>(new("--no-http-cache")
    {
        Description = CommandDefinitionStrings.CmdNoCacheOptionDescription,
        Arity = ArgumentArity.Zero,
        Hidden = hidden
    }, forward);

    public readonly Option<bool> IgnoreFailedSourcesOption = ForwardWhen<bool>(new("--ignore-failed-sources")
    {
        Description = CommandDefinitionStrings.CmdIgnoreFailedSourcesOptionDescription,
        Arity = ArgumentArity.Zero,
        Hidden = hidden
    }, forward);

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption(hidden: hidden);

    private static Option<T> ForwardWhen<T>(Option<T> option, bool forward)
        => forward ? option.Forward() : option;

    public void AddTo(IList<Option> options)
    {
        options.Add(DisableParallelOption);
        options.Add(IgnoreFailedSourcesOption);
        options.Add(NoCacheOption);
        options.Add(NoHttpCacheOption);
        options.Add(InteractiveOption);
    }
}

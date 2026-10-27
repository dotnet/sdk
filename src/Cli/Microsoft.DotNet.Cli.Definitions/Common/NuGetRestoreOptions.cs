// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands;

internal sealed class NuGetRestoreOptions(bool hidden = false, bool forward = false)
{
    internal const string NoCacheEnvironmentVariableName = "NO_CACHE";

    public readonly Option<bool> DisableParallelOption = ForwardWhen<bool>(new("--disable-parallel")
    {
        Description = CommandDefinitionStrings.CmdDisableParallelOptionDescription,
        Arity = ArgumentArity.Zero,
        Hidden = hidden
    }, forward);

    public readonly Option<bool> NoCacheOption = ForwardWhenEnabled(new("--no-cache")
    {
        Description = CommandDefinitionStrings.CmdNoCacheOptionDescription,
        Hidden = true,
        Arity = ArgumentArity.Zero,
        DefaultValueFactory = _ => EnvironmentVariableParser.ParseBool(
            Environment.GetEnvironmentVariable(NoCacheEnvironmentVariableName),
            defaultValue: false),
        CustomParser = _ => true,
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

    private static Option<bool> ForwardWhenEnabled(Option<bool> option, bool forward)
        => forward ? option.ForwardIfEnabled(option.Name) : option;

    public void AddTo(IList<Option> options)
    {
        options.Add(DisableParallelOption);
        options.Add(IgnoreFailedSourcesOption);
        options.Add(NoCacheOption);
        options.Add(NoHttpCacheOption);
        options.Add(InteractiveOption);
    }
}

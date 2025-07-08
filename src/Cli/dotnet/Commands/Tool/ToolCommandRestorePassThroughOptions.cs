// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal static class ToolCommandRestorePassThroughOptions
{
    public static Option<bool> DisableParallelOption = new ForwardedOption<bool>("--disable-parallel")
    {
        Description = CliCommandStrings.CmdDisableParallelOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--disable-parallel");

    public static Option<bool> NoCacheOption = new ForwardedOption<bool>("--no-cache")
    {
        Description = CliCommandStrings.CmdNoCacheOptionDescription,
        Hidden = true,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--no-cache");

    public static Option<bool> NoHttpCacheOption = new ForwardedOption<bool>("--no-http-cache")
    {
        Description = CliCommandStrings.CmdNoCacheOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--no-http-cache");

    public static Option<bool> IgnoreFailedSourcesOption = new ForwardedOption<bool>("--ignore-failed-sources")
    {
        Description = CliCommandStrings.CmdIgnoreFailedSourcesOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--ignore-failed-sources");

    public static Option<bool> InteractiveRestoreOption = CommonOptions.InteractiveOption();
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandRestorePassThroughOptions
    {
        public static Option<bool> DisableParallelOption = new ForwardedOption<bool>("--disable-parallel")
        {
            Description = LocalizableStrings.CmdDisableParallelOptionDescription
        }.ForwardAs("--disable-parallel");

        public static Option<bool> NoCacheOption = new ForwardedOption<bool>("--no-cache")
        {
            Description = LocalizableStrings.CmdNoCacheOptionDescription,
            Hidden = true
        }.ForwardAs("--no-cache");

        public static Option<bool> NoHttpCacheOption = new ForwardedOption<bool>("--no-http-cache")
        {
            Description = LocalizableStrings.CmdNoCacheOptionDescription
        }.ForwardAs("--no-http-cache");

        public static Option<bool> IgnoreFailedSourcesOption = new ForwardedOption<bool>("--ignore-failed-sources")
        {
            Description = LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription
        }.ForwardAs("--ignore-failed-sources");

        public static Option<bool> InteractiveRestoreOption = new ForwardedOption<bool>("--interactive")
        {
            Description = CommonLocalizableStrings.CommandInteractiveOptionDescription
        }.ForwardAs(Constants.RestoreInteractiveOption);
    }
}

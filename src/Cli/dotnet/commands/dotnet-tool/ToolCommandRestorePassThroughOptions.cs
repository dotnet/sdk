// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolCommandRestorePassThroughOptions
    {
        public static CliOption<bool> DisableParallelOption = new ForwardedOption<bool>("--disable-parallel")
        {
            Description = LocalizableStrings.CmdDisableParallelOptionDescription
        }.ForwardAs("--disable-parallel");

        public static CliOption<bool> NoCacheOption = new ForwardedOption<bool>("--no-cache")
        {
            Description = LocalizableStrings.CmdNoCacheOptionDescription
        }.ForwardAs("--no-cache");

        public static CliOption<bool> IgnoreFailedSourcesOption = new ForwardedOption<bool>("--ignore-failed-sources")
        {
            Description = LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription
        }.ForwardAs("--ignore-failed-sources");

        public static CliOption<bool> InteractiveRestoreOption = new ForwardedOption<bool>("--interactive")
        {
            Description = CommonLocalizableStrings.CommandInteractiveOptionDescription
        }.ForwardAs(Constants.RestoreInteractiveOption);
    }
}

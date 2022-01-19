// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;
using System.CommandLine;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCommandNuGetRestoreActionConfigOptions
    {
        public static Option<bool> DisableParallelOption = new ForwardedOption<bool>(
            "--disable-parallel",
            LocalizableStrings.CmdDisableParallelOptionDescription);

        public static Option<bool> NoCacheOption = new ForwardedOption<bool>(
            "--no-cache",
            LocalizableStrings.CmdNoCacheOptionDescription);

        public static Option<bool> IgnoreFailedSourcesOption = new ForwardedOption<bool>(
            "--ignore-failed-sources",
            LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription);

        public static Option<bool> InteractiveRestoreOption = new ForwardedOption<bool>(
            "--interactive",
            CommonLocalizableStrings.CommandInteractiveOptionDescription);

        public static Option<bool> HiddenDisableParallelOption = new ForwardedOption<bool>(
            "--disable-parallel",
            LocalizableStrings.CmdDisableParallelOptionDescription).Hide();

        public static Option<bool> HiddenNoCacheOption = new ForwardedOption<bool>(
            "--no-cache",
            LocalizableStrings.CmdNoCacheOptionDescription).Hide();

        public static Option<bool> HiddenIgnoreFailedSourcesOption = new ForwardedOption<bool>(
            "--ignore-failed-sources",
            LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription).Hide();

        public static Option<bool> HiddenInteractiveRestoreOption = new ForwardedOption<bool>(
            "--interactive",
            CommonLocalizableStrings.CommandInteractiveOptionDescription).Hide();

        public static RestoreActionConfig ToRestoreActionConfig(this ParseResult parseResult)
        {
            return new RestoreActionConfig(
                DisableParallel: parseResult.HasOption(DisableParallelOption) ? parseResult.ValueForOption<bool>(DisableParallelOption) : parseResult.ValueForOption<bool>(HiddenDisableParallelOption),
                NoCache: parseResult.HasOption(NoCacheOption) ? parseResult.ValueForOption<bool>(NoCacheOption) : parseResult.ValueForOption<bool>(HiddenNoCacheOption),
                IgnoreFailedSources: parseResult.HasOption(IgnoreFailedSourcesOption) ? parseResult.ValueForOption<bool>(IgnoreFailedSourcesOption) : parseResult.ValueForOption<bool>(HiddenIgnoreFailedSourcesOption),
                Interactive: parseResult.HasOption(InteractiveRestoreOption) ? parseResult.ValueForOption<bool>(InteractiveRestoreOption) : parseResult.ValueForOption<bool>(HiddenInteractiveRestoreOption));
        }

        public static void AddWorkloadCommandNuGetRestoreActionConfigOptions(this Command command, bool Hide = false)
        {
            command.AddOption(Hide ? HiddenDisableParallelOption : DisableParallelOption);
            command.AddOption(Hide ? HiddenIgnoreFailedSourcesOption : IgnoreFailedSourcesOption);
            command.AddOption(Hide ? HiddenNoCacheOption : NoCacheOption);
            command.AddOption(Hide ? HiddenInteractiveRestoreOption : InteractiveRestoreOption);
        }
    }
}

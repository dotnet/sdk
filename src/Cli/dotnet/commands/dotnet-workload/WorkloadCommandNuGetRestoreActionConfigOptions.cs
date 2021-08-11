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

        public static RestoreActionConfig ToRestoreActionConfig(this ParseResult parseResult)
        {
            return new RestoreActionConfig(DisableParallel: parseResult.ValueForOption<bool>(DisableParallelOption),
                NoCache: parseResult.ValueForOption<bool>(NoCacheOption),
                IgnoreFailedSources: parseResult.ValueForOption<bool>(IgnoreFailedSourcesOption),
                Interactive: parseResult.ValueForOption<bool>(InteractiveRestoreOption));
        }

        public static void AddWorkloadCommandNuGetRestoreActionConfigOptions(this Command command)
        {
            command.AddOption(DisableParallelOption);
            command.AddOption(IgnoreFailedSourcesOption);
            command.AddOption(NoCacheOption);
            command.AddOption(InteractiveRestoreOption);
        }
    }
}

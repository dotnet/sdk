// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUpdateCommandParser
    {
        public static readonly Option ConfigOption = WorkloadInstallCommandParser.ConfigOption;

        public static readonly Option AddSourceOption = WorkloadInstallCommandParser.AddSourceOption;

        public static readonly Option VersionOption = WorkloadInstallCommandParser.VersionOption;

        public static readonly Option VerbosityOption = WorkloadInstallCommandParser.VerbosityOption;

        public static readonly Option IncludePreviewsOption = WorkloadInstallCommandParser.IncludePreviewOption;

        public static readonly Option DownloadToCacheOption = WorkloadInstallCommandParser.DownloadToCacheOption;

        public static readonly Option TempDirOption = WorkloadInstallCommandParser.TempDirOption;

        public static readonly Option PrintDownloadLinkOnlyOption =
            WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption;

        public static readonly Option FromCacheOption =
            WorkloadInstallCommandParser.FromCacheOption;

        public static readonly Option FromPreviousSdkOption = new Option<bool>("--from-previous-sdk", LocalizableStrings.FromPreviousSdkOptionDescription);

        public static Command GetCommand()
        {
            Command command = new("update", LocalizableStrings.CommandDescription);

            command.AddOption(ConfigOption);
            command.AddOption(AddSourceOption);
            command.AddOption(VersionOption);
            command.AddOption(PrintDownloadLinkOnlyOption);
            command.AddOption(FromCacheOption);
            command.AddOption(IncludePreviewsOption);
            command.AddOption(DownloadToCacheOption);
            command.AddOption(TempDirOption);
            command.AddOption(FromPreviousSdkOption);
            command.AddOption(WorkloadCommandNuGetRestoreActionConfigOptions.DisableParallelOption);
            command.AddOption(WorkloadCommandNuGetRestoreActionConfigOptions.IgnoreFailedSourcesOption);
            command.AddOption(WorkloadCommandNuGetRestoreActionConfigOptions.NoCacheOption);
            command.AddOption(WorkloadCommandNuGetRestoreActionConfigOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);

            return command;
        }
    }
}

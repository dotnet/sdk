// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Update;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUpdateCommandParser
    {
        public static readonly CliOption<string> TempDirOption = WorkloadInstallCommandParser.TempDirOption;

        public static readonly CliOption<bool> FromPreviousSdkOption = new("--from-previous-sdk")
        {
            Description = LocalizableStrings.FromPreviousSdkOptionDescription
        };

        public static readonly CliOption<bool> AdManifestOnlyOption = new("--advertising-manifests-only")
        {
            Description = LocalizableStrings.AdManifestOnlyOptionDescription
        };

        public static readonly CliOption<bool> PrintRollbackOption = new("--print-rollback")
        {
            Hidden = true
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("update", LocalizableStrings.CommandDescription);

            InstallingWorkloadCommandParser.AddWorkloadInstallCommandOptions(command);

            command.Options.Add(TempDirOption);
            command.Options.Add(FromPreviousSdkOption);
            command.Options.Add(AdManifestOnlyOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(PrintRollbackOption);
            command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);

            command.SetAction((parseResult) => new WorkloadUpdateCommand(parseResult).Execute());

            return command;
        }
    }
}

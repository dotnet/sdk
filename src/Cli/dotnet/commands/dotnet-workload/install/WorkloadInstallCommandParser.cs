// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Install;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadInstallCommandParser
    {
        public static readonly Argument<IEnumerable<string>> WorkloadIdArgument = new("workloadId")
        {
            HelpName = LocalizableStrings.WorkloadIdArgumentName,
            Arity = ArgumentArity.OneOrMore,
            Description = LocalizableStrings.WorkloadIdArgumentDescription
        };

        public static readonly Option<bool> SkipSignCheckOption = new("--skip-sign-check")
        {
            Description = LocalizableStrings.SkipSignCheckOptionDescription,
            Hidden = true
        };

        public static readonly Option<bool> SkipManifestUpdateOption = new("--skip-manifest-update")
        {
            Description = LocalizableStrings.SkipManifestUpdateOptionDescription
        };

        public static readonly Option<string> TempDirOption = new("--temp-dir")
        {
            Description = LocalizableStrings.TempDirOptionDescription
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("install", LocalizableStrings.CommandDescription);

            command.Arguments.Add(WorkloadIdArgument);
            AddWorkloadInstallCommandOptions(command);

            command.SetAction((parseResult) => new WorkloadInstallCommand(parseResult).Execute());

            return command;
        }

        internal static void AddWorkloadInstallCommandOptions(Command command)
        {
            InstallingWorkloadCommandParser.AddWorkloadInstallCommandOptions(command);

            command.Options.Add(SkipManifestUpdateOption);
            command.Options.Add(TempDirOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(SkipSignCheckOption);
            command.Options.Add(InstallingWorkloadCommandParser.WorkloadSetVersionOption);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Config;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadConfigCommandParser
    {
        //  dotnet workload config --update-mode workload-set

        public static readonly string UpdateMode_WorkloadSet = "workload-set";
        public static readonly string UpdateMode_Manifests = "manifests";

        public static readonly CliOption<string> UpdateMode = new("--update-mode")
        {
            Description = LocalizableStrings.UpdateModeDescription,
            Arity = ArgumentArity.ZeroOrOne
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            UpdateMode.AcceptOnlyFromAmong(UpdateMode_WorkloadSet, UpdateMode_Manifests);

            CliCommand command = new("config", LocalizableStrings.CommandDescription);
            command.Options.Add(UpdateMode);

            command.SetAction(parseResult =>
            {
                new WorkloadConfigCommand(parseResult).Execute();
            });

            return command;
        }
    }
}

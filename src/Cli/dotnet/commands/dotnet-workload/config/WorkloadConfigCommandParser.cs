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

        public static readonly Option<string> UpdateMode = new("--update-mode")
        {
            Description = LocalizableStrings.UpdateModeDescription,
            Arity = ArgumentArity.ZeroOrOne
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            UpdateMode.AcceptOnlyFromAmong(UpdateMode_WorkloadSet, UpdateMode_Manifests);

            Command command = new("config", LocalizableStrings.CommandDescription);
            command.Options.Add(UpdateMode);

            command.SetAction(parseResult =>
            {
                new WorkloadConfigCommand(parseResult).Execute();
            });

            return command;
        }
    }
}

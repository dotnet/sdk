// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Elevate;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Elevate.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadElevateCommandParser
    {
        /// <summary>
        /// Optional, hidden argument supplied by the unelevated client at server launch with the value of
        /// the client's <see cref="System.IO.Path.GetTempPath"/>. Used by the elevated server to accept
        /// IPC-supplied paths that originate from the client's temp directory when it differs from the
        /// server's (e.g., over-the-shoulder UAC, custom TEMP env vars).
        /// </summary>
        public static readonly CliOption<string> ClientTempOption = new("--client-temp")
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
            CliCommand command = new("elevate", LocalizableStrings.CommandDescription)
            {
                Hidden = true
            };

            command.Options.Add(ClientTempOption);

            command.SetAction((parseResult) => new WorkloadElevateCommand(parseResult).Execute());

            return command;
        }
    }
}

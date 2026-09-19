// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Elevate;

internal sealed class WorkloadElevateCommandDefinition : WorkloadCommandDefinitionBase
{
    /// <summary>
    /// Optional, hidden argument supplied by the unelevated client at server launch with the value of
    /// the client's <see cref="System.IO.Path.GetTempPath"/>. Used by the elevated server to accept
    /// IPC-supplied paths that originate from the client's temp directory when it differs from the
    /// server's (e.g., over-the-shoulder UAC, custom TEMP env vars).
    /// </summary>
    public readonly Option<string> ClientTempOption = new("--client-temp")
    {
        Hidden = true
    };

    public WorkloadElevateCommandDefinition()
        : base("elevate", CommandDefinitionStrings.WorkloadElevateCommandDescription)
    {
        Hidden = true;
        Options.Add(ClientTempOption);
    }
}

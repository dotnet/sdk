// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Clean;

internal static class WorkloadCleanCommandParser
{
    public static WorkloadCleanCommandDefinition ConfigureCommand(WorkloadCleanCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadCleanCommand(parseResult).Execute());
        return def;
    }
}

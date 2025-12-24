// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.History;

internal static class WorkloadHistoryCommandParser
{
    public static WorkloadHistoryCommandDefinition ConfigureCommand(WorkloadHistoryCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadHistoryCommand(parseResult).Execute());
        return def;
    }
}

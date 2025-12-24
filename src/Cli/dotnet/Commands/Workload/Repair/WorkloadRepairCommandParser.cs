// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.Repair;

internal static class WorkloadRepairCommandParser
{
    public static WorkloadRepairCommandDefinition ConfigureCommand(WorkloadRepairCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadRepairCommand(parseResult).Execute());
        return def;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.Restore;

internal static class WorkloadRestoreCommandParser
{
    public static WorkloadRestoreCommandDefinition ConfigureCommand(WorkloadRestoreCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadRestoreCommand(parseResult).Execute());
        return def;
    }
}

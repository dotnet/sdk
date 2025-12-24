// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.List;

internal static class WorkloadListCommandParser
{
    public static WorkloadListCommandDefinition ConfigureCommand(WorkloadListCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadListCommand(parseResult).Execute());
        return def;
    }
}

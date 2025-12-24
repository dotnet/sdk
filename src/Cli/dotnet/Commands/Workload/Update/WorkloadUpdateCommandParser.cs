// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.Update;

internal static class WorkloadUpdateCommandParser
{
    public static WorkloadUpdateCommandDefinition ConfigureCommand(WorkloadUpdateCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadUpdateCommand(parseResult).Execute());
        return def;
    }
}

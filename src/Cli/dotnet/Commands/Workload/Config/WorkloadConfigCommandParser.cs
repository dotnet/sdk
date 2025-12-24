// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.Config;

internal static class WorkloadConfigCommandParser
{
    public static WorkloadConfigCommandDefinition ConfigureCommand(WorkloadConfigCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadConfigCommand(parseResult).Execute());
        return def;
    }
}

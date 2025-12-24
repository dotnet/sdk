// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.Elevate;

internal static class WorkloadElevateCommandParser
{
    public static WorkloadElevateCommandDefinition ConfigureCommand(WorkloadElevateCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadElevateCommand(parseResult).Execute());
        return def;
    }
}

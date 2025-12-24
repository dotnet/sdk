// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.Install;

internal static class WorkloadInstallCommandParser
{
    public static WorkloadInstallCommandDefinition ConfigureCommand(WorkloadInstallCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadInstallCommand(parseResult).Execute());
        return def;
    }
}


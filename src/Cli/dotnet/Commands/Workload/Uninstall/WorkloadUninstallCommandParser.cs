// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.Uninstall;

internal static class WorkloadUninstallCommandParser
{
    public static WorkloadUninstallCommandDefinition ConfigureCommand(WorkloadUninstallCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadUninstallCommand(parseResult).Execute());
        return def;
    }
}

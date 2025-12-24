// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Workload.Search;

internal static class WorkloadSearchCommandParser
{
    public static WorkloadSearchCommandDefinition ConfigureCommand(WorkloadSearchCommandDefinition def)
    {
        def.SetAction(parseResult => new WorkloadSearchCommand(parseResult).Execute());
        def.VersionCommand.SetAction(parseResult => new WorkloadSearchVersionsCommand(parseResult).Execute());
        return def;
    }
}

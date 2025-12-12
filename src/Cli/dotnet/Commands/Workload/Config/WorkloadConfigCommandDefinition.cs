// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Config;

internal static class WorkloadConfigCommandDefinition
{
    //  dotnet workload config --update-mode workload-set

    public static readonly string UpdateMode_WorkloadSet = "workload-set";
    public static readonly string UpdateMode_Manifests = "manifests";

    public static readonly Option<string> UpdateMode = new("--update-mode")
    {
        Description = CliCommandStrings.UpdateModeDescription,
        Arity = ArgumentArity.ZeroOrOne
    };

    public static Command Create()
    {
        UpdateMode.AcceptOnlyFromAmong(UpdateMode_WorkloadSet, UpdateMode_Manifests);

        Command command = new("config", CliCommandStrings.WorkloadConfigCommandDescription);
        command.Options.Add(UpdateMode);

        return command;
    }
}

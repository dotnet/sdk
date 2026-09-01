// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Workload.Config;

internal sealed class WorkloadConfigCommandDefinition : WorkloadCommandDefinitionBase
{
    //  dotnet workload config --update-mode workload-set

    public const string UpdateMode_WorkloadSet = "workload-set";
    public const string UpdateMode_Manifests = "manifests";

    public readonly Option<string> UpdateMode = new("--update-mode")
    {
        Description = CommandDefinitionStrings.UpdateModeDescription,
        Arity = ArgumentArity.ZeroOrOne
    };

    public WorkloadConfigCommandDefinition()
        : base("config", CommandDefinitionStrings.WorkloadConfigCommandDescription)
    {
        UpdateMode.AcceptOnlyFromAmong(UpdateMode_WorkloadSet, UpdateMode_Manifests);

        Options.Add(UpdateMode);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Update;

internal sealed class WorkloadUpdateCommandDefinition : InstallingWorkloadCommandDefinition
{
    public const string FromHistoryOptionName = "--from-history";

    public readonly Option<bool> FromPreviousSdkOption = new("--from-previous-sdk")
    {
        Description = CommandDefinitionStrings.FromPreviousSdkOptionDescription
    };

    public readonly Option<bool> AdManifestOnlyOption = new("--advertising-manifests-only")
    {
        Description = CommandDefinitionStrings.AdManifestOnlyOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> PrintRollbackOption = new("--print-rollback")
    {
        Hidden = true,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<int> FromHistoryOption = new(FromHistoryOptionName)
    {
        Description = CommandDefinitionStrings.FromHistoryOptionDescription
    };

    public readonly Option<string> HistoryManifestOnlyOption = new("--manifests-only")
    {
        Description = CommandDefinitionStrings.HistoryManifestOnlyOptionDescription
    };

    public WorkloadUpdateCommandDefinition()
        : base("update", CommandDefinitionStrings.WorkloadUpdateCommandDescription)
    {
        Options.Add(FromPreviousSdkOption);
        Options.Add(AdManifestOnlyOption);
        Options.Add(PrintRollbackOption);
        Options.Add(FromHistoryOption);
        Options.Add(HistoryManifestOnlyOption);
    }
}

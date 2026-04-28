// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Clean;

internal sealed class WorkloadCleanCommandDefinition : WorkloadCommandDefinitionBase
{
    public readonly Option<bool> CleanAllOption = new("--all")
    {
        Description = CommandDefinitionStrings.CleanAllOptionDescription
    };

    public readonly Option<string> SdkVersionOption = CreateSdkVersionOption();

    public WorkloadCleanCommandDefinition()
        : base("clean", CommandDefinitionStrings.WorkloadCleanCommandDescription)
    {
        Options.Add(CleanAllOption);
    }
}

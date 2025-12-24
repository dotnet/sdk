// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Workload.Clean;

internal sealed class WorkloadCleanCommandDefinition : WorkloadCommandDefinitionBase
{
    public readonly Option<bool> CleanAllOption = new("--all")
    {
        Description = CliCommandStrings.CleanAllOptionDescription
    };

    public readonly Option<string> SdkVersionOption = CreateSdkVersionOption();

    public WorkloadCleanCommandDefinition()
        : base("clean", CliCommandStrings.WorkloadCleanCommandDescription)
    {
        Options.Add(CleanAllOption);
    }
}

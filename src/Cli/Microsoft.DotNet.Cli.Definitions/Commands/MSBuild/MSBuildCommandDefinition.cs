// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

internal sealed class MSBuildCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-msbuild";

    public new readonly Argument<string[]> Arguments = new("arguments");
    public readonly Option<string[]?> TargetOption = CommonOptions.CreateMSBuildTargetOption();
    public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();

    public MSBuildCommandDefinition()
        : base("msbuild", CommandDefinitionStrings.BuildAppFullName)
    {
        this.DocsLink = Link;
        base.Arguments.Add(Arguments);

        Options.Add(DisableBuildServersOption);
        Options.Add(TargetOption);
    }
}

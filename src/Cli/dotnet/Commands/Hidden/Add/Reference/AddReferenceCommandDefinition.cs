// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Reference.Add;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;

internal sealed class AddReferenceCommandDefinition : ReferenceAddCommandDefinitionBase
{
    public new const string Name = "reference";

    public readonly Option<string> ProjectOption = ReferenceCommandDefinition.CreateProjectOption();

    public AddReferenceCommandDefinition()
        : base(Name)
    {
        Options.Add(ProjectOption);
    }

    public override string? GetFileOrDirectory(ParseResult parseResult)
        => parseResult.HasOption(ProjectOption)
        ? parseResult.GetValue(ProjectOption)
        : parseResult.GetValue(Parent.ProjectOrFileArgument);

    public AddCommandDefinition Parent => (AddCommandDefinition)Parents.Single();
}

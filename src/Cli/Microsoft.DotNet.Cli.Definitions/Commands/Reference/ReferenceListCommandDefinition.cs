// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal sealed class ReferenceListCommandDefinition : ListReferenceCommandDefinitionBase
{
    public new const string Name = "list";

    public readonly Option<string?> FileOption = ReferenceCommandDefinition.CreateFileOption();

    public ReferenceListCommandDefinition()
        : base(Name)
    {
        Options.Add(FileOption);
    }

    public ReferenceCommandDefinition Parent => (ReferenceCommandDefinition)Parents.Single();

    internal override Option<string?>? GetFileOption() => FileOption;

    internal override Option<string?>? GetProjectOption() => Parent.ProjectOption;

    internal override Argument<string>? GetProjectOrFileArgument() => null;
}

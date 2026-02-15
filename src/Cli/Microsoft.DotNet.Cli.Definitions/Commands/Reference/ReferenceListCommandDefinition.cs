// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal sealed class ReferenceListCommandDefinition()
    : ListReferenceCommandDefinitionBase(Name)
{
    public new const string Name = "list";

    public ReferenceCommandDefinition Parent => (ReferenceCommandDefinition)Parents.Single();

    internal override string? GetFileOrDirectory(ParseResult parseResult)
        => parseResult.GetValue(Parent.ProjectOption);
}

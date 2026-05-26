// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove.Reference;

internal sealed class RemoveReferenceCommandDefinition() : ReferenceRemoveCommandDefinitionBase(Name)
{
    public new const string Name = "reference";

    public RemoveCommandDefinition Parent => (RemoveCommandDefinition)Parents.Single();

    public override string? GetFileOrDirectory(ParseResult parseResult)
        => parseResult.GetValue(Parent.ProjectOrFileArgument);

    public override AppKinds GetAllowedAppKinds(ParseResult parseResult)
        => AppKinds.Any;
}

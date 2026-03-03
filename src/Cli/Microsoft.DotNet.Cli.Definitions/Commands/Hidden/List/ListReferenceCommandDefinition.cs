// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;

internal sealed class ListReferenceCommandDefinition : ListReferenceCommandDefinitionBase
{
    public new const string Name = "reference";

    public readonly Argument<string> Argument = new("argument") { Arity = ArgumentArity.ZeroOrOne, Hidden = true };

    public ListReferenceCommandDefinition() : base(Name)
    {
        Arguments.Add(Argument);
    }

    public ListCommandDefinition Parent => (ListCommandDefinition)Parents.Single();

    internal override string? GetFileOrDirectory(ParseResult parseResult)
        => parseResult.GetValue(Parent.SlnOrProjectArgument);
}

internal abstract class ListReferenceCommandDefinitionBase : Command
{
    public ListReferenceCommandDefinitionBase(string name)
        : base(name, CommandDefinitionStrings.ReferenceListAppFullName)
    {
    }

    internal abstract string? GetFileOrDirectory(ParseResult parseResult);
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal sealed class ReferenceRemoveCommandDefinition() : ReferenceRemoveCommandDefinitionBase(Name)
{
    public new const string Name = "remove";

    public ReferenceCommandDefinition Parent => (ReferenceCommandDefinition)Parents.Single();

    public override string? GetFileOrDirectory(ParseResult parseResult)
        => parseResult.GetValue(Parent.ProjectOption);
}

internal abstract class ReferenceRemoveCommandDefinitionBase : Command
{
    public static Argument<IEnumerable<string>> CreateProjectPathArgument() => new(CommandDefinitionStrings.ReferenceRemoveProjectPathArgumentName)
    {
        Description = CommandDefinitionStrings.ReferenceRemoveProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
    };

    public static Option<string> CreateFrameworkOption() => new("--framework", "-f")
    {
        Description = CommandDefinitionStrings.ReferenceRemoveCmdFrameworkDescription,
        HelpName = CommandDefinitionStrings.CommonCmdFramework
    };

    public readonly Argument<IEnumerable<string>> ProjectPathArgument = CreateProjectPathArgument();
    public readonly Option<string> FrameworkOption = CreateFrameworkOption();

    public ReferenceRemoveCommandDefinitionBase(string name)
        : base(name, CommandDefinitionStrings.ReferenceRemoveAppFullName)
    {
        Arguments.Add(ProjectPathArgument);
        Options.Add(FrameworkOption);
    }

    public abstract string? GetFileOrDirectory(ParseResult parseResult);
}

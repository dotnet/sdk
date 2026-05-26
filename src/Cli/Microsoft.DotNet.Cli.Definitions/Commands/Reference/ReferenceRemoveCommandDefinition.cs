// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Commands.Reference.Remove;

internal sealed class ReferenceRemoveCommandDefinition : ReferenceRemoveCommandDefinitionBase
{
    public new const string Name = "remove";

    public readonly Option<string?> FileOption = ReferenceCommandDefinition.CreateFileOption();

    public ReferenceRemoveCommandDefinition()
        : base(Name)
    {
        Options.Add(FileOption);
    }

    public ReferenceCommandDefinition Parent => (ReferenceCommandDefinition)Parents.Single();

    public override string? GetFileOrDirectory(ParseResult parseResult)
    {
        if (parseResult.HasOption(FileOption))
        {
            return parseResult.GetValue(FileOption);
        }

        return parseResult.GetValue(Parent.ProjectOption);
    }

    public override AppKinds GetAllowedAppKinds(ParseResult parseResult)
        => parseResult.HasOption(FileOption) ? AppKinds.FileBased : AppKinds.ProjectBased;
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

    public abstract AppKinds GetAllowedAppKinds(ParseResult parseResult);
}

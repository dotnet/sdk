// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Commands.Reference.Add;

internal sealed class ReferenceAddCommandDefinition : ReferenceAddCommandDefinitionBase
{
    public new const string Name = "add";

    public readonly Option<string?> FileOption = ReferenceCommandDefinition.CreateFileOption();

    public ReferenceAddCommandDefinition()
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

    public override (string? FileOptionName, string? ProjectOptionName) GetConflictingPathOptions(ParseResult parseResult)
        => parseResult.HasOption(FileOption) && parseResult.HasOption(Parent.ProjectOption)
            ? (FileOption.Name, Parent.ProjectOption.Name)
            : (null, null);
}

internal abstract class ReferenceAddCommandDefinitionBase : Command
{
    public readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CommandDefinitionStrings.ReferenceAddProjectPathArgumentName)
    {
        Description = CommandDefinitionStrings.ReferenceAddProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
        CustomParser = arguments =>
        {
            var result = arguments.Tokens.TakeWhile(t => !t.Value.StartsWith("-"));
            arguments.OnlyTake(result.Count());
            return result.Select(t => t.Value);
        }
    };

    public readonly Option<string> FrameworkOption = new("--framework", "-f")
    {
        Description = CommandDefinitionStrings.ReferenceAddCmdFrameworkDescription,
        HelpName = CommandDefinitionStrings.CommonCmdFramework,
        IsDynamic = true,
    };

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption();

    public ReferenceAddCommandDefinitionBase(string name)
        : base(name, CommandDefinitionStrings.ReferenceAddAppFullName)
    {
        Arguments.Add(ProjectPathArgument);
        Options.Add(FrameworkOption);
        Options.Add(InteractiveOption);
    }

    public abstract string? GetFileOrDirectory(ParseResult parseResult);

    public abstract AppKinds GetAllowedAppKinds(ParseResult parseResult);

    public abstract (string? FileOptionName, string? ProjectOptionName) GetConflictingPathOptions(ParseResult parseResult);
}

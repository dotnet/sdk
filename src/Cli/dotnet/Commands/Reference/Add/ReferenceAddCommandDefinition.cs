// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Add;

internal sealed class ReferenceAddCommandDefinition() : ReferenceAddCommandDefinitionBase(Name)
{
    public new const string Name = "add";

    public ReferenceCommandDefinition Parent => (ReferenceCommandDefinition)Parents.Single();

    public override string? GetFileOrDirectory(ParseResult parseResult)
        => parseResult.GetValue(Parent.ProjectOption);
}

internal abstract class ReferenceAddCommandDefinitionBase : Command
{
    public static Argument<IEnumerable<string>> CreateProjectPathArgument() => new(CliCommandStrings.ReferenceAddProjectPathArgumentName)
    {
        Description = CliCommandStrings.ReferenceAddProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
        CustomParser = arguments =>
        {
            var result = arguments.Tokens.TakeWhile(t => !t.Value.StartsWith("-"));
            arguments.OnlyTake(result.Count());
            return result.Select(t => t.Value);
        }
    };

    public static Option<string> CreateFrameworkOption() => new Option<string>("--framework", "-f")
    {
        Description = CliCommandStrings.ReferenceAddCmdFrameworkDescription,
        HelpName = CliStrings.CommonCmdFramework,
        IsDynamic = true,
    }
    .AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);

    public readonly Argument<IEnumerable<string>> ProjectPathArgument = CreateProjectPathArgument();
    public readonly Option<string> FrameworkOption = CreateFrameworkOption();
    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption();

    public ReferenceAddCommandDefinitionBase(string name)
        : base(name, CliCommandStrings.ReferenceAddAppFullName)
    {
        Arguments.Add(ProjectPathArgument);
        Options.Add(FrameworkOption);
        Options.Add(InteractiveOption);
    }

    public abstract string? GetFileOrDirectory(ParseResult parseResult);
}

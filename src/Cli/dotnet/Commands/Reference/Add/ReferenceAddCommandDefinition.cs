// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Add;

internal static class ReferenceAddCommandDefinition
{
    public const string Name = "add";

    public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.ReferenceAddProjectPathArgumentName)
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

    public static readonly Option<string> FrameworkOption = new Option<string>("--framework", "-f")
    {
        Description = CliCommandStrings.ReferenceAddCmdFrameworkDescription,
        HelpName = CliStrings.CommonCmdFramework,
        IsDynamic = true,
    }
    .AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption();

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.ReferenceAddAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(FrameworkOption);
        command.Options.Add(InteractiveOption);

        return command;
    }
}

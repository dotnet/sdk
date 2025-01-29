// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Reference.Add;
using LocalizableStrings = Microsoft.DotNet.Tools.Reference.Add.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ReferenceAddCommandParser
    {
        public static readonly CliArgument<IEnumerable<string>> ProjectPathArgument = new(LocalizableStrings.ProjectPathArgumentName)
        {
            Description = LocalizableStrings.ProjectPathArgumentDescription,
            Arity = ArgumentArity.OneOrMore,
            CustomParser = arguments => {
                var result = arguments.Tokens.TakeWhile(t => File.Exists(t.Value));
                arguments.OnlyTake(result.Count());
                return result.Select(t => t.Value);
            }
        };

        public static readonly CliOption<string> FrameworkOption = new CliOption<string>("--framework", "-f")
        {
            Description = LocalizableStrings.CmdFrameworkDescription,
            HelpName = CommonLocalizableStrings.CmdFramework

        }.AddCompletions(Complete.TargetFrameworksFromProjectFile);

        public static readonly CliOption<bool> InteractiveOption = CommonOptions.InteractiveOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("add", LocalizableStrings.AppFullName);

            command.Arguments.Add(ProjectPathArgument);
            command.Options.Add(FrameworkOption);
            command.Options.Add(InteractiveOption);

            command.SetAction((parseResult) => new AddProjectToProjectReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

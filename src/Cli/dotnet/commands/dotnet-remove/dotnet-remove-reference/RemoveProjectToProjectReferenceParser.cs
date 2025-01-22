// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Remove.ProjectToProjectReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemoveProjectToProjectReferenceParser
    {
        public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new Argument<IEnumerable<string>>(LocalizableStrings.ProjectPathArgumentName)
        {
            Description = LocalizableStrings.ProjectPathArgumentDescription,
            Arity = ArgumentArity.OneOrMore,
        }.AddCompletions(Complete.ProjectReferencesFromProjectFile);

        public static readonly Option<string> FrameworkOption = new("--framework", "-f")
        {
            Description = LocalizableStrings.CmdFrameworkDescription,
            HelpName = CommonLocalizableStrings.CmdFramework
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("reference", LocalizableStrings.AppFullName);

            command.Arguments.Add(ProjectPathArgument);
            command.Options.Add(FrameworkOption);

            command.SetAction((parseResult) => new RemoveProjectToProjectReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

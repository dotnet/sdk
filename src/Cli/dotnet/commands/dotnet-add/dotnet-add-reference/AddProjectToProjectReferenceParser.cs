// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Add.ProjectToProjectReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddProjectToProjectReferenceParser
    {
        public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(LocalizableStrings.ProjectPathArgumentName)
        {
            Description = LocalizableStrings.ProjectPathArgumentDescription,
            Arity = ArgumentArity.OneOrMore
        };

        public static readonly Option<string> FrameworkOption = new Option<string>("--framework", "-f")
        {
            Description = LocalizableStrings.CmdFrameworkDescription,
            HelpName = Tools.Add.PackageReference.LocalizableStrings.CmdFramework

        }.AddCompletions(Complete.TargetFrameworksFromProjectFile);

        public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("reference", LocalizableStrings.AppFullName);

            command.Arguments.Add(ProjectPathArgument);
            command.Options.Add(FrameworkOption);
            command.Options.Add(InteractiveOption);

            command.SetAction((parseResult) => new AddProjectToProjectReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

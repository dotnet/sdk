// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Sln.Add;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnAddParser
    {
        public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(LocalizableStrings.AddProjectPathArgumentName)
        {
            HelpName = LocalizableStrings.AddProjectPathArgumentName,
            Description = LocalizableStrings.AddProjectPathArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly Option<bool> InRootOption = new("--in-root")
        {
            Description = LocalizableStrings.InRoot
        };

        public static readonly Option<string> SolutionFolderOption = new("--solution-folder", "-s")
        {
            Description = LocalizableStrings.AddProjectSolutionFolderArgumentDescription
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("add", LocalizableStrings.AddAppFullName);

            command.Arguments.Add(ProjectPathArgument);
            command.Options.Add(InRootOption);
            command.Options.Add(SolutionFolderOption);

            command.SetAction((parseResult) => new AddProjectToSolutionCommand(parseResult).Execute());

            return command;
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools.Sln.Add;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnAddParser
    {
        public static readonly CliArgument<IEnumerable<string>> ProjectPathArgument = new(LocalizableStrings.AddProjectPathArgumentName)
        {
            HelpName = LocalizableStrings.AddProjectPathArgumentName,
            Description = LocalizableStrings.AddProjectPathArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly CliOption<bool> InRootOption = new("--in-root")
        {
            Description = LocalizableStrings.InRoot
        };

        public static readonly CliOption<string> SolutionFolderOption = new("--solution-folder", "-s")
        {
            Description = LocalizableStrings.AddProjectSolutionFolderArgumentDescription
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("add", LocalizableStrings.AddAppFullName);

            command.Arguments.Add(ProjectPathArgument);
            command.Options.Add(InRootOption);
            command.Options.Add(SolutionFolderOption);

            command.SetAction((parseResult) => new AddProjectToSolutionCommand(parseResult).Execute());

            return command;
        }
    }
}

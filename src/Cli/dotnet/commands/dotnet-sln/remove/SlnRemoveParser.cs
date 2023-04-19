// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools.Sln.Remove;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnRemoveParser
    {
        public static readonly CliArgument<IEnumerable<string>> ProjectPathArgument = new(LocalizableStrings.RemoveProjectPathArgumentName)
        {
            HelpName = LocalizableStrings.RemoveProjectPathArgumentName,
            Description = LocalizableStrings.RemoveProjectPathArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("remove", LocalizableStrings.RemoveAppFullName);

            command.Arguments.Add(ProjectPathArgument);

            command.SetAction((parseResult) => new RemoveProjectFromSolutionCommand(parseResult).Execute());

            return command;
        }
    }
}

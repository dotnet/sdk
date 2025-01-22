// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Sln.Remove;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnRemoveParser
    {
        public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new(LocalizableStrings.RemoveProjectPathArgumentName)
        {
            HelpName = LocalizableStrings.RemoveProjectPathArgumentName,
            Description = LocalizableStrings.RemoveProjectPathArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("remove", LocalizableStrings.RemoveAppFullName);

            command.Arguments.Add(ProjectPathArgument);

            command.SetAction((parseResult) => new RemoveProjectFromSolutionCommand(parseResult).Execute());

            return command;
        }
    }
}

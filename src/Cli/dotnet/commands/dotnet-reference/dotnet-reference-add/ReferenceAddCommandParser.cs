// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Add.ProjectToProjectReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ReferenceAddCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("add", LocalizableStrings.AppFullName);

            command.Arguments.Add(AddProjectToProjectReferenceParser.ProjectPathArgument);
            command.Options.Add(AddProjectToProjectReferenceParser.FrameworkOption);
            command.Options.Add(AddProjectToProjectReferenceParser.InteractiveOption);

            command.SetAction((parseResult) => new AddProjectToProjectReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

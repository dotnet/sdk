// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Remove.ProjectToProjectReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ReferenceRemoveCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("remove", LocalizableStrings.AppFullName);

            command.Arguments.Add(RemoveProjectToProjectReferenceParser.ProjectPathArgument);
            command.Options.Add(RemoveProjectToProjectReferenceParser.FrameworkOption);

            command.SetAction((parseResult) => new RemoveProjectToProjectReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

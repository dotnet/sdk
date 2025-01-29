// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Reference.List;
using LocalizableStrings = Microsoft.DotNet.Tools.Reference.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ReferenceListCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("list", LocalizableStrings.AppFullName);

            command.Options.Add(ReferenceCommandParser.ProjectOption);

            command.SetAction((parseResult) => new ListProjectToProjectReferencesCommand(parseResult).Execute());

            return command;
        }
    }
}

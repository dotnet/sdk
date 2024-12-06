// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Remove.PackageReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageRemoveCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("remove", LocalizableStrings.AppFullName);

            command.Arguments.Add(RemovePackageParser.CmdPackageArgument);
            command.Options.Add(RemovePackageParser.InteractiveOption);

            command.SetAction((parseResult) => new RemovePackageReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

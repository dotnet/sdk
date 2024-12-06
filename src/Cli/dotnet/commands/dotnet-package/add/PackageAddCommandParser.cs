// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Add.PackageReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageAddCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("add", LocalizableStrings.AppFullName);

            command.Arguments.Add(AddPackageParser.CmdPackageArgument);
            command.Options.Add(AddPackageParser.VersionOption);
            command.Options.Add(AddPackageParser.FrameworkOption);
            command.Options.Add(AddPackageParser.NoRestoreOption);
            command.Options.Add(AddPackageParser.SourceOption);
            command.Options.Add(AddPackageParser.PackageDirOption);
            command.Options.Add(AddPackageParser.InteractiveOption);
            command.Options.Add(AddPackageParser.PrereleaseOption);

            command.SetAction((parseResult) => new AddPackageReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Package.Add;
using LocalizableStrings = Microsoft.DotNet.Tools.Package.Add.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddPackageParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("package", LocalizableStrings.AppFullName);

            command.Arguments.Add(PackageAddCommandParser.CmdPackageArgument);
            command.Options.Add(PackageAddCommandParser.VersionOption);
            command.Options.Add(PackageAddCommandParser.FrameworkOption);
            command.Options.Add(PackageAddCommandParser.NoRestoreOption);
            command.Options.Add(PackageAddCommandParser.SourceOption);
            command.Options.Add(PackageAddCommandParser.PackageDirOption);
            command.Options.Add(PackageAddCommandParser.InteractiveOption);
            command.Options.Add(PackageAddCommandParser.PrereleaseOption);
            command.Options.Add(PackageCommandParser.ProjectOption);

            command.SetAction((parseResult) => new AddPackageReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

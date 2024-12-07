// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Add.PackageReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.PackageReference.LocalizableStrings;

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

            command.SetAction((parseResult) => new AddPackageReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}

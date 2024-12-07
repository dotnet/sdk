// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.List.PackageReferences;
using LocalizableStrings = Microsoft.DotNet.Tools.List.PackageReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListPackageReferencesCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("package", LocalizableStrings.AppFullName);

            command.Options.Add(PackageListCommandParser.VerbosityOption);
            command.Options.Add(PackageListCommandParser.OutdatedOption);
            command.Options.Add(PackageListCommandParser.DeprecatedOption);
            command.Options.Add(PackageListCommandParser.VulnerableOption);
            command.Options.Add(PackageListCommandParser.FrameworkOption);
            command.Options.Add(PackageListCommandParser.TransitiveOption);
            command.Options.Add(PackageListCommandParser.PrereleaseOption);
            command.Options.Add(PackageListCommandParser.HighestPatchOption);
            command.Options.Add(PackageListCommandParser.HighestMinorOption);
            command.Options.Add(PackageListCommandParser.ConfigOption);
            command.Options.Add(PackageListCommandParser.SourceOption);
            command.Options.Add(PackageListCommandParser.InteractiveOption);
            command.Options.Add(PackageListCommandParser.FormatOption);
            command.Options.Add(PackageListCommandParser.OutputVersionOption);

            command.SetAction((parseResult) => new ListPackageReferencesCommand(parseResult).Execute());

            return command;
        }
    }
}

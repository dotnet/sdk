// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.List.PackageReferences;
using LocalizableStrings = Microsoft.DotNet.Tools.List.PackageReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageListCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("list", LocalizableStrings.AppFullName);

            command.Options.Add(ListPackageReferencesCommandParser.VerbosityOption);
            command.Options.Add(ListPackageReferencesCommandParser.OutdatedOption);
            command.Options.Add(ListPackageReferencesCommandParser.DeprecatedOption);
            command.Options.Add(ListPackageReferencesCommandParser.VulnerableOption);
            command.Options.Add(ListPackageReferencesCommandParser.FrameworkOption);
            command.Options.Add(ListPackageReferencesCommandParser.TransitiveOption);
            command.Options.Add(ListPackageReferencesCommandParser.PrereleaseOption);
            command.Options.Add(ListPackageReferencesCommandParser.HighestPatchOption);
            command.Options.Add(ListPackageReferencesCommandParser.HighestMinorOption);
            command.Options.Add(ListPackageReferencesCommandParser.ConfigOption);
            command.Options.Add(ListPackageReferencesCommandParser.SourceOption);
            command.Options.Add(ListPackageReferencesCommandParser.InteractiveOption);
            command.Options.Add(ListPackageReferencesCommandParser.FormatOption);
            command.Options.Add(ListPackageReferencesCommandParser.OutputVersionOption);

            command.SetAction((parseResult) => new ListPackageReferencesCommand(parseResult).Execute());

            return command;
        }
    }
}

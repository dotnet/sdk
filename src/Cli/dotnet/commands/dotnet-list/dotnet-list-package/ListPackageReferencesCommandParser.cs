// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.List.PackageReferences;
using LocalizableStrings = Microsoft.DotNet.Tools.List.PackageReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListPackageReferencesCommandParser
    {
        public static readonly Option OutdatedOption = new ForwardedOption<bool>("--outdated", LocalizableStrings.CmdOutdatedDescription)
            .ForwardAs("--outdated");

        public static readonly Option DeprecatedOption = new ForwardedOption<bool>("--deprecated", LocalizableStrings.CmdDeprecatedDescription)
            .ForwardAs("--deprecated");

        public static readonly Option VulnerableOption = new ForwardedOption<bool>("--vulnerable", LocalizableStrings.CmdVulnerableDescription)
            .ForwardAs("--vulnerable");

        public static readonly Option FrameworkOption = new ForwardedOption<IEnumerable<string>>("--framework", LocalizableStrings.CmdFrameworkDescription)
        {
            HelpName = LocalizableStrings.CmdFramework
        }.ForwardAsManyArgumentsEachPrefixedByOption("--framework")
        .AllowSingleArgPerToken();

        public static readonly Option TransitiveOption = new ForwardedOption<bool>("--include-transitive", LocalizableStrings.CmdTransitiveDescription)
            .ForwardAs("--include-transitive");

        public static readonly Option PrereleaseOption = new ForwardedOption<bool>("--include-prerelease", LocalizableStrings.CmdPrereleaseDescription)
            .ForwardAs("--include-prerelease");

        public static readonly Option HighestPatchOption = new ForwardedOption<bool>("--highest-patch", LocalizableStrings.CmdHighestPatchDescription)
            .ForwardAs("--highest-patch");

        public static readonly Option HighestMinorOption = new ForwardedOption<bool>("--highest-minor", LocalizableStrings.CmdHighestMinorDescription)
            .ForwardAs("--highest-minor");

        public static readonly Option ConfigOption = new ForwardedOption<string>("--config", LocalizableStrings.CmdConfigDescription)
        {
            HelpName = LocalizableStrings.CmdConfig
        }.ForwardAsMany(o => new[] { "--config", o });

        public static readonly Option SourceOption = new ForwardedOption<IEnumerable<string>>("--source", LocalizableStrings.CmdSourceDescription)
        {
            HelpName = LocalizableStrings.CmdSource
        }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
        .AllowSingleArgPerToken();

        public static readonly Option InteractiveOption = new ForwardedOption<bool>("--interactive", CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs("--interactive");

        public static readonly Option VerbosityOption = new ForwardedOption<VerbosityOptions>(
                new string[] { "-v", "--verbosity" },
                description: CommonLocalizableStrings.VerbosityOptionDescription)
            {
                HelpName = CommonLocalizableStrings.LevelArgumentName
            }.ForwardAsSingle(o => $"--verbosity:{o}");

        public static readonly Option FormatOption = new ForwardedOption<ReportOutputFormat>("--format", LocalizableStrings.CmdFormatDescription)
        { }.ForwardAsSingle(o => $"--format:{o}");

        public static readonly Option OutputVersionOption = new ForwardedOption<int>("--output-version", LocalizableStrings.CmdOutputVersionDescription)
        { }.ForwardAsSingle(o => $"--output-version:{o}");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("package", LocalizableStrings.AppFullName);

            command.Options.Add(VerbosityOption);
            command.Options.Add(OutdatedOption);
            command.Options.Add(DeprecatedOption);
            command.Options.Add(VulnerableOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(TransitiveOption);
            command.Options.Add(PrereleaseOption);
            command.Options.Add(HighestPatchOption);
            command.Options.Add(HighestMinorOption);
            command.Options.Add(ConfigOption);
            command.Options.Add(SourceOption);
            command.Options.Add(InteractiveOption);
            command.Options.Add(FormatOption);
            command.Options.Add(OutputVersionOption);

            command.SetAction((parseResult) => new ListPackageReferencesCommand(parseResult).Execute());

            return command;
        }
    }
}

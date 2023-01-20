// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Publish;
using LocalizableStrings = Microsoft.DotNet.Tools.Publish.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PublishCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-publish";

        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option<string> OuputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.OutputOption
        }.ForwardAsSingle(o => $"-property:PublishDir={CommandDirectoryContext.GetFullPath(o)}");

        public static readonly Option<IEnumerable<string>> ManifestOption = new ForwardedOption<IEnumerable<string>>("--manifest", LocalizableStrings.ManifestOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.ManifestOption
        }.ForwardAsSingle(o => $"-property:TargetManifestFiles={string.Join("%3B", o.Select(CommandDirectoryContext.GetFullPath))}")
        .AllowSingleArgPerToken();

        public static readonly Option<bool> NoBuildOption = new ForwardedOption<bool>("--no-build", LocalizableStrings.NoBuildOptionDescription)
            .ForwardAs("-property:NoBuild=true");

        public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-nologo");

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly Option<bool> SelfContainedOption = CommonOptions.SelfContainedOption;

        public static readonly Option<bool> NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

        public static readonly Option<string> RuntimeOption = CommonOptions.RuntimeOption;

        public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly Option<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("publish", DocsLink, LocalizableStrings.AppDescription);

            command.Arguments.Add(SlnOrProjectArgument);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: true);
            command.Options.Add(OuputOption);
            command.Options.Add(ManifestOption);
            command.Options.Add(NoBuildOption);
            command.Options.Add(SelfContainedOption);
            command.Options.Add(NoSelfContainedOption);
            command.Options.Add(NoLogoOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.Options.Add(ConfigurationOption);
            command.Options.Add(CommonOptions.VersionSuffixOption);
            command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
            command.Options.Add(NoRestoreOption);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(CommonOptions.ArchitectureOption);
            command.Options.Add(CommonOptions.OperatingSystemOption);
            command.Options.Add(CommonOptions.DisableBuildServersOption);

            command.SetHandler(PublishCommand.Run);

            return command;
        }
    }
}

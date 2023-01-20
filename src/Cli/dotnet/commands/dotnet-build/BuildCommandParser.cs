// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Build;
using LocalizableStrings = Microsoft.DotNet.Tools.Build.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-build";

        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option<string> OutputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.OutputOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.OutputOptionName
        }.ForwardAsSingle(arg => $"-property:OutputPath={CommandDirectoryContext.GetFullPath(arg)}");

        public static readonly Option<bool> NoIncrementalOption = new Option<bool>("--no-incremental", LocalizableStrings.NoIncrementalOptionDescription);

        public static readonly Option<bool> NoDependenciesOption = new ForwardedOption<bool>("--no-dependencies", LocalizableStrings.NoDependenciesOptionDescription)
            .ForwardAs("-property:BuildProjectReferences=false");

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
            var command = new DocumentedCommand("build", DocsLink, LocalizableStrings.AppFullName);

            command.Arguments.Add(SlnOrProjectArgument);
            RestoreCommandParser.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: false);
            command.Options.Add(FrameworkOption);
            command.Options.Add(ConfigurationOption);
            command.Options.Add(RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.Options.Add(CommonOptions.VersionSuffixOption);
            command.Options.Add(NoRestoreOption);
            command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(CommonOptions.DebugOption);
            command.Options.Add(OutputOption);
            command.Options.Add(NoIncrementalOption);
            command.Options.Add(NoDependenciesOption);
            command.Options.Add(NoLogoOption);
            command.Options.Add(SelfContainedOption);
            command.Options.Add(NoSelfContainedOption);
            command.Options.Add(CommonOptions.ArchitectureOption);
            command.Options.Add(CommonOptions.OperatingSystemOption);
            command.Options.Add(CommonOptions.DisableBuildServersOption);

            command.SetHandler(BuildCommand.Run);

            return command;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Update;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUpdateCommandParser
    {
        public static readonly CliArgument<string> PackageIdArgument = ToolInstallCommandParser.PackageIdArgument;

        public static readonly CliOption<bool> GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly CliOption<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

        public static readonly CliOption<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly CliOption<string> ConfigOption = ToolInstallCommandParser.ConfigOption;

        public static readonly CliOption<string[]> AddSourceOption = ToolInstallCommandParser.AddSourceOption;

        public static readonly CliOption<string> FrameworkOption = ToolInstallCommandParser.FrameworkOption;

        public static readonly CliOption<string> VersionOption = ToolInstallCommandParser.VersionOption;

        public static readonly CliOption<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        public static readonly CliOption<bool> PrereleaseOption = ToolSearchCommandParser.PrereleaseOption;

        public static readonly CliOption<VerbosityOptions> VerbosityOption = ToolInstallCommandParser.VerbosityOption;

        public static readonly CliOption<bool> AllowPackageDowngradeOption = ToolInstallCommandParser.AllowPackageDowngradeOption;

        public static readonly CliOption<bool> AllUpdateOption = new("--all")
        {
            Description = LocalizableStrings.AllUpdateOptionDescription
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("update", LocalizableStrings.CommandDescription);
            // TBD: whether to include argument based on if --all
            // TBD: errors occurs if empty argument is passed when creating toolLocalUpdate and toolGlobalUpdate
            command.Arguments.Add(PackageIdArgument);

            ToolInstallCommandParser.AddCommandOptions(command);
            command.Options.Add(AllowPackageDowngradeOption);
            command.Options.Add(AllUpdateOption);

            command.SetAction((parseResult) => new ToolUpdateCommand(parseResult).Execute());

            return command;
        }
    }
}

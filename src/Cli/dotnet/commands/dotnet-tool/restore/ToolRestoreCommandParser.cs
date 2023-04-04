// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Restore;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRestoreCommandParser
    {
        public static readonly CliOption<string> ConfigOption = ToolInstallCommandParser.ConfigOption;

        public static readonly CliOption<string[]> AddSourceOption = ToolInstallCommandParser.AddSourceOption;

        public static readonly CliOption<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        public static readonly CliOption<VerbosityOptions> VerbosityOption = ToolInstallCommandParser.VerbosityOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("restore", LocalizableStrings.CommandDescription);

            command.Options.Add(ConfigOption);
            command.Options.Add(AddSourceOption);
            command.Options.Add(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ManifestPathOptionDescription));
            command.Options.Add(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.Options.Add(VerbosityOption);

            command.SetAction((parseResult) => new ToolRestoreCommand(parseResult).Execute());

            return command;
        }
    }
}

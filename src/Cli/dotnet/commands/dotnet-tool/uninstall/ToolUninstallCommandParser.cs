// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUninstallCommandParser
    {
        public static readonly CliArgument<string> PackageIdArgument = ToolInstallCommandParser.PackageIdArgument;

        public static readonly CliOption<bool> GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly CliOption<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly CliOption<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

        public static readonly CliOption<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("uninstall", LocalizableStrings.CommandDescription);

            command.Arguments.Add(PackageIdArgument);
            command.Options.Add(GlobalOption.WithHelpDescription(command, LocalizableStrings.GlobalOptionDescription));
            command.Options.Add(LocalOption.WithHelpDescription(command, LocalizableStrings.LocalOptionDescription));
            command.Options.Add(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolPathOptionDescription));
            command.Options.Add(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ManifestPathOptionDescription));

            command.SetAction((parseResult) => new ToolUninstallCommand(parseResult).Execute());

            return command;
        }
    }
}

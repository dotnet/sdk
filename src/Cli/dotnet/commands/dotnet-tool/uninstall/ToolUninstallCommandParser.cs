// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUninstallCommandParser
    {
        public static readonly Argument<string> PackageIdArgument = ToolInstallCommandParser.PackageIdArgument;

        public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

        public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("uninstall", LocalizableStrings.CommandDescription);

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

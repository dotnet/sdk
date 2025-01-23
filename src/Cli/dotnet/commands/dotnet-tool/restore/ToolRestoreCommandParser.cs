// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Restore;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRestoreCommandParser
    {
        public static readonly Option<string> ConfigOption = ToolInstallCommandParser.ConfigOption;

        public static readonly Option<string[]> AddSourceOption = ToolInstallCommandParser.AddSourceOption;

        public static readonly Option<string> ToolManifestOption = ToolAppliedOption.ToolManifestOption;

        public static readonly Option<VerbosityOptions> VerbosityOption = ToolInstallCommandParser.VerbosityOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("restore", LocalizableStrings.CommandDescription);

            command.Options.Add(ConfigOption);
            command.Options.Add(AddSourceOption);
            command.Options.Add(ToolManifestOption.WithHelpDescription(command, LocalizableStrings.ManifestPathOptionDescription));
            command.Options.Add(ToolCommandRestorePassThroughOptions.DisableParallelOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.NoCacheOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.NoHttpCacheOption);
            command.Options.Add(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.Options.Add(VerbosityOption);

            command.SetAction((parseResult) => new ToolRestoreCommand(parseResult).Execute());

            return command;
        }
    }
}

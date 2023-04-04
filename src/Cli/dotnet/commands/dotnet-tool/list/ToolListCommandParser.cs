// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.List;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolListCommandParser
    {
        public static readonly CliArgument<string> PackageIdArgument = new("packageId")
        {
            HelpName = LocalizableStrings.PackageIdArgumentName,
            Description = LocalizableStrings.PackageIdArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne,
        };

        public static readonly CliOption<bool> GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly CliOption<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly CliOption<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("list", LocalizableStrings.CommandDescription);

            command.Arguments.Add(PackageIdArgument);
            command.Options.Add(GlobalOption.WithHelpDescription(command, LocalizableStrings.GlobalOptionDescription));
            command.Options.Add(LocalOption.WithHelpDescription(command, LocalizableStrings.LocalOptionDescription));
            command.Options.Add(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolPathOptionDescription));

            command.SetAction((parseResult) => new ToolListCommand(parseResult).Execute());

            return command;
        }
    }
}

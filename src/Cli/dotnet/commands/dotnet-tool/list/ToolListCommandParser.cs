// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.List;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolListCommandParser
    {
        public static readonly Argument<string> PackageIdArgument = new("packageId")
        {
            HelpName = LocalizableStrings.PackageIdArgumentName,
            Description = LocalizableStrings.PackageIdArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne,
        };

        public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption;

        public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption;

        public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

        public static readonly Option<ToolListOutputFormat> ToolListFormatOption = new("--format")
        {
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => ToolListOutputFormat.table,
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("list", LocalizableStrings.CommandDescription);

            command.Arguments.Add(PackageIdArgument);
            command.Options.Add(GlobalOption.WithHelpDescription(command, LocalizableStrings.GlobalOptionDescription));
            command.Options.Add(LocalOption.WithHelpDescription(command, LocalizableStrings.LocalOptionDescription));
            command.Options.Add(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolPathOptionDescription));
            command.Options.Add(ToolListFormatOption.WithHelpDescription(command, LocalizableStrings.ToolListFormatOptionDescription));

            command.SetAction((parseResult) => new ToolListCommand(parseResult).Execute());

            return command;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Extensions;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Commands.Tool.List;

internal static class ToolListCommandParser
{
    public static readonly CliArgument<string> PackageIdArgument = new("packageId")
    {
        HelpName = LocalizableStrings.ToolListPackageIdArgumentName,
        Description = LocalizableStrings.ToolListPackageIdArgumentDescription,
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly CliOption<bool> GlobalOption = ToolAppliedOption.GlobalOption;

    public static readonly CliOption<bool> LocalOption = ToolAppliedOption.LocalOption;

    public static readonly CliOption<string> ToolPathOption = ToolAppliedOption.ToolPathOption;

    public static readonly CliOption<ToolListOutputFormat> ToolListFormatOption = new("--format")
    {
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => ToolListOutputFormat.table,
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("list", LocalizableStrings.ToolListCommandDescription);

        command.Arguments.Add(PackageIdArgument);
        command.Options.Add(GlobalOption.WithHelpDescription(command, LocalizableStrings.ToolListGlobalOptionDescription));
        command.Options.Add(LocalOption.WithHelpDescription(command, LocalizableStrings.ToolListLocalOptionDescription));
        command.Options.Add(ToolPathOption.WithHelpDescription(command, LocalizableStrings.ToolListToolPathOptionDescription));
        command.Options.Add(ToolListFormatOption.WithHelpDescription(command, LocalizableStrings.ToolListFormatOptionDescription));

        command.SetAction((parseResult) => new ToolListCommand(parseResult).Execute());

        return command;
    }
}

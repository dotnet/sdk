// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool.List;

internal static class ToolListCommandParser
{
    public static readonly Argument<string> PackageIdArgument = new("packageId")
    {
        HelpName = CliCommandStrings.ToolListPackageIdArgumentName,
        Description = CliCommandStrings.ToolListPackageIdArgumentDescription,
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<bool> GlobalOption = ToolAppliedOption.GlobalOption(CliCommandStrings.ToolListGlobalOptionDescription);

    public static readonly Option<bool> LocalOption = ToolAppliedOption.LocalOption(CliCommandStrings.ToolListLocalOptionDescription);

    public static readonly Option<string> ToolPathOption = ToolAppliedOption.ToolPathOption(CliCommandStrings.ToolListToolPathOptionDescription);

    public static readonly Option<ToolListOutputFormat> ToolListFormatOption = new("--format")
    {
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => ToolListOutputFormat.table,
        Description = CliCommandStrings.ToolListFormatOptionDescription
    };

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("list", CliCommandStrings.ToolListCommandDescription);

        command.Arguments.Add(PackageIdArgument);
        command.Options.Add(GlobalOption);
        command.Options.Add(LocalOption);
        command.Options.Add(ToolPathOption);
        command.Options.Add(ToolListFormatOption);

        command.SetAction((parseResult) => new ToolListCommand(parseResult).Execute());

        return command;
    }
}

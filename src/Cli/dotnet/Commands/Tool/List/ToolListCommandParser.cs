// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Tool.List;

internal static class ToolListCommandParser
{
    public static readonly Argument<string> PackageIdArgument = ToolListCommandDefinition.PackageIdArgument;

    public static readonly Option<bool> GlobalOption = ToolListCommandDefinition.GlobalOption;

    public static readonly Option<bool> LocalOption = ToolListCommandDefinition.LocalOption;

    public static readonly Option<string> ToolPathOption = ToolListCommandDefinition.ToolPathOption;

    public static readonly Option<ToolListOutputFormat> ToolListFormatOption = ToolListCommandDefinition.ToolListFormatOption;

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = ToolListCommandDefinition.Create();

        command.SetAction((parseResult) => new ToolListCommand(parseResult).Execute());

        return command;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

internal static class ListCommandParser
{
    public static Option<OutputFormat> FormatOption => CommonOptions.FormatOption;

    public static readonly Option<bool> NoVerifyOption = new("--no-verify")
    {
        Description = Strings.ListNoVerifyOptionDescription,
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Command ListCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return ListCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("list", Strings.ListCommandDescription);

        command.Options.Add(FormatOption);
        command.Options.Add(NoVerifyOption);

        command.SetAction(parseResult => new ListCommand(parseResult).Execute());

        return command;
    }
}

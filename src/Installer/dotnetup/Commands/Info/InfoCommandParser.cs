// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

internal static class InfoCommandParser
{
    public static Option<OutputFormat> FormatOption => CommonOptions.FormatOption;

    public static readonly Option<bool> NoListOption = new("--no-list")
    {
        Description = Strings.InfoNoListOptionDescription,
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Command InfoCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return InfoCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("--info", Strings.InfoOptionDescription);

        command.Options.Add(FormatOption);
        command.Options.Add(NoListOption);

        command.SetAction(parseResult =>
        {
            var format = parseResult.GetValue(FormatOption);
            var noList = parseResult.GetValue(NoListOption);
            return Info.InfoCommand.Execute(format, noList);
        });

        return command;
    }
}

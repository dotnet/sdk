// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvClearCommandParser
{
    public static Command ConstructCommand()
    {
        Command command = new("clear", Strings.EnvClearCommandDescription);
        command.Options.Add(CommonOptions.ShellOption);
        command.SetAction(parseResult => new EnvClearCommand(parseResult).Execute());
        return command;
    }
}

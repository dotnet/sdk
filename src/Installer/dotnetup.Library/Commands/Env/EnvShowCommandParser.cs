// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvShowCommandParser
{
    public static Command ConstructCommand()
    {
        Command command = new("show", Strings.EnvShowCommandDescription);
        command.Options.Add(CommonOptions.ShellOption);
        command.SetAction(parseResult => new EnvShowCommand(parseResult).Execute());
        return command;
    }
}

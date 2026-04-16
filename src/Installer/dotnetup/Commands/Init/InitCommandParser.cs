// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

internal static class InitCommandParser
{
    private static readonly Command s_command = ConstructCommand();

    public static Command GetCommand() => s_command;

    private static Command ConstructCommand()
    {
        Command command = new("init", Strings.InitCommandDescription);

        command.Options.Add(CommonOptions.InstallPathOption);
        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.NoProgressOption);
        command.Options.Add(CommonOptions.VerbosityOption);
        command.Options.Add(CommonOptions.RequireMuxerUpdateOption);

        command.SetAction(parseResult => new InitCommand(parseResult).Execute());

        return command;
    }
}

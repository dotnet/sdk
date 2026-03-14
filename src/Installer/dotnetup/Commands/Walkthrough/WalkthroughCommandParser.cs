// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Walkthrough;

internal static class WalkthroughCommandParser
{
    private static readonly Command s_command = ConstructCommand();

    public static Command GetCommand() => s_command;

    private static Command ConstructCommand()
    {
        Command command = new("walkthrough", Strings.WalkthroughCommandDescription);

        command.Options.Add(CommonOptions.InstallPathOption);
        command.Options.Add(CommonOptions.ManifestPathOption);
        command.Options.Add(CommonOptions.NoProgressOption);
        command.Options.Add(CommonOptions.RequireMuxerUpdateOption);

        command.SetAction(parseResult => new WalkthroughCommand(parseResult).Execute());

        return command;
    }
}

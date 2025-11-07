// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Complete;

internal static partial class CompleteCommandParser
{
    public static readonly Argument<string> PathArgument = new("path");

    public static readonly Option<int?> PositionOption = new("--position")
    {
        HelpName = "command"
    };

    public static Command CreateCommandDefinition()
    {
        Command command = new("complete")
        {
            Hidden = true
        };

        command.Arguments.Add(PathArgument);
        command.Options.Add(PositionOption);

        return command;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Parse;

internal static partial class ParseCommandParser
{

    public static Command CreateCommandDefinition()
    {
        var command = new Command("parse")
        {
            Hidden = true
        };

        return command;
    }
}

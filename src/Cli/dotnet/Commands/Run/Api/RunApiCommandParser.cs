// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run.Api;

internal sealed partial class RunApiCommandParser
{
    public static Command CreateCommandDefinition()
    {
        Command command = new("run-api")
        {
            Hidden = true,
        };
        return command;
    }
}

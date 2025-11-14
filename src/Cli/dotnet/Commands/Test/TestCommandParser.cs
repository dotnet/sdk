// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestCommandParser
{
    private static readonly Command Command = TestCommandDefinition.Create();

    public static Command GetCommand()
    {
        return Command;
    }
}

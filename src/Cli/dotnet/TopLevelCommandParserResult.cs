// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli;

internal class TopLevelCommandParserResult(string command)
{
    public static TopLevelCommandParserResult Empty
    {
        get { return new TopLevelCommandParserResult(string.Empty); }
    }

    public string Command { get; } = command;
}

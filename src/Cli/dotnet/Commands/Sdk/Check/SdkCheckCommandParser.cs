// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Check;

internal static partial class SdkCheckCommandParser
{

    public static Command CreateCommandDefinition()
    {
        Command command = new("check", CliCommandStrings.SdkCheckAppFullName);

        return command;
    }
}

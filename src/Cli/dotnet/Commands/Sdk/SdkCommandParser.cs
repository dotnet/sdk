// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Sdk;

internal static partial class SdkCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-sdk";

    public static Command CreateCommandDefinition()
    {
        Command command = new("sdk", CliCommandStrings.SdkAppFullName)
        {
            DocsLink = DocsLink
        };
        command.Subcommands.Add(SdkCheckCommandParser.GetCommand());

        return command;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;

namespace Microsoft.DotNet.Cli.Commands.Sdk;

internal static class SdkCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-sdk";

    public static Command Create()
    {
        Command command = new("sdk", CliCommandStrings.SdkAppFullName)
        {
            DocsLink = DocsLink
        };
        command.Subcommands.Add(SdkCheckCommandDefinition.Create());

        return command;
    }
}

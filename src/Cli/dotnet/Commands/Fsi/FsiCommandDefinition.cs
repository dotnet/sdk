// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Fsi;

internal static class FsiCommandDefinition
{
    public const string Name = "fsi";

    public static readonly string DocsLink = "https://aka.ms/dotnet-fsi";

    public static readonly Argument<string[]> Arguments = new("arguments");

    public static Command Create()
    {
        Command command = new(Name) {
            Arguments = { Arguments },
            DocsLink = DocsLink,
        };

        return command;
    }
}

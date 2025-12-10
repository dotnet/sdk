// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Format;

internal static class FormatCommandDefinition
{
    public const string Name = "format";

    public static readonly Argument<string[]> Arguments = new("arguments");

    public static readonly string DocsLink = "https://aka.ms/dotnet-format";

    public static Command Create()
        => new(Name)
        {
            Arguments = { Arguments },
            DocsLink = DocsLink,
        };
}

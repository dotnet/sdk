// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Fsi;

internal sealed class FsiCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-fsi";

    public new readonly Argument<string[]> Arguments = new("arguments");

    public FsiCommandDefinition()
        : base("fsi")
    {
        this.DocsLink = Link;
        base.Arguments.Add(Arguments);
    }
}

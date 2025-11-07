// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.VSTest;

internal static partial class VSTestCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-vstest";

    public static Command CreateCommandDefinition()
    {
        Command command = new("vstest")
        {
            TreatUnmatchedTokensAsErrors = false,
            DocsLink = DocsLink
        };

        command.Options.Add(CommonOptions.TestPlatformOption);
        command.Options.Add(CommonOptions.TestFrameworkOption);
        command.Options.Add(CommonOptions.TestLoggerOption);

        return command;
    }
}

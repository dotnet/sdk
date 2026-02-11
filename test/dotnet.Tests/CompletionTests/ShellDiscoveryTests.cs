// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable


using System.CommandLine.StaticCompletions.Shells;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Microsoft.Extensions.Options;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace System.CommandLine.StaticCompletions.Tests;

public class ShellDiscoveryTests
{
    [Fact]
    public void StaticCompletionsCanParseWithoutAShell()
    {
        ParseResult result = Parser.Parse(["dotnet", "completions", "script"]);
        result.Errors.Should().BeEmpty();
        result.GetValue<IShellProvider>("@CompletionsCommand_ShellArgument_Description").Should().NotBeNull();
    }
}

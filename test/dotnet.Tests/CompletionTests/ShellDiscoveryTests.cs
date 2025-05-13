// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable


using System.CommandLine.StaticCompletions.Shells;
using Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace System.CommandLine.StaticCompletions.Tests;

public class ShellDiscoveryTests()
{
    [Fact]
    public void StaticCompletionsCanParseWithoutAShell()
    {
        var result = Parser.Instance.Parse(@"dotnet completions script");
        result.Errors.Should().BeEmpty();
        result.GetValue<IShellProvider>("@CompletionsCommand_ShellArgument_Description").Should().NotBeNull();
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Tests.ParserTests;

public class CommonOptionsTests
{
    [Fact]
    public void Duplicates()
    {
        var command = new CliRootCommand();
        command.Options.Add(CommonOptions.EnvOption);

        var result = command.Parse(["-e", "A=1", "-e", "A=2"]);

        result.GetValue(CommonOptions.EnvOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = "2" });

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void MultiplePerToken()
    {
        var command = new CliRootCommand();
        command.Options.Add(CommonOptions.EnvOption);

        var result = command.Parse(["-e", "A=1;B=2,C=3 D=4", "-e", "B==Y=", "-e", "C;=;"]);

        result.GetValue(CommonOptions.EnvOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string>
            {
                ["A"] = "1;B=2,C=3 D=4",
                ["B"] = "=Y=",
                ["C;"] = ";"
            });

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void WhitespaceTrimming()
    {
        var command = new CliRootCommand();
        command.Options.Add(CommonOptions.EnvOption);

        var result = command.Parse(["-e", " A \t\n\r\u2002 = X Y \t\n\r\u2002"]);

        result.GetValue(CommonOptions.EnvOption)
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["A"] = " X Y \t\n\r\u2002" });

        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("=")]
    [InlineData("= X")]
    [InlineData("  \u2002 = X")]
    public void Errors(string token)
    {
        var command = new CliRootCommand();
        command.Options.Add(CommonOptions.EnvOption);

        var result = command.Parse(["-e", token]);

        result.Errors.Select(e => e.Message).Should().BeEquivalentTo(
        [
            string.Format(CommonLocalizableStrings.IncorrectlyFormattedEnvironmentVariables, token)
        ]);
    }
}

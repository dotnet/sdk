// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class InfoCommandTests
{
    [Fact]
    public void Parser_ShouldParseInfoOption()
    {
        // Arrange
        var args = new[] { "--info" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.GetValue(Parser.InfoOption).Should().BeTrue();
        // Note: Parser may report "Required command was not provided" error,
        // but we handle --info before subcommand validation in Program.Main
    }

    [Theory]
    [InlineData("--info", "--json")]
    [InlineData("--json", "--info")]
    public void Parser_ShouldParseInfoWithJsonOption_OrderIndependent(string first, string second)
    {
        // Arrange
        var args = new[] { first, second };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.GetValue(Parser.InfoOption).Should().BeTrue();
        parseResult.GetValue(InfoCommandParser.JsonOption).Should().BeTrue();
    }

    [Fact]
    public void Parser_ShouldParseInfoWithNoListOption()
    {
        // Arrange
        var args = new[] { "--info", "--no-list" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.GetValue(Parser.InfoOption).Should().BeTrue();
        parseResult.GetValue(InfoCommandParser.NoListOption).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InfoCommand_ShouldReturnZeroExitCode(bool jsonOutput)
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act - use noList: true to avoid manifest access in unit tests
            var exitCode = InfoCommand.Execute(jsonOutput: jsonOutput, noList: true);

            // Assert
            exitCode.Should().Be(0);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void InfoCommand_HumanReadable_ShouldOutputExpectedFormat()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act - use noList: true to avoid manifest access in unit tests
            InfoCommand.Execute(jsonOutput: false, noList: true);
            var output = sw.ToString();

            // Assert
            output.Should().Contain("dotnetup Information:");
            output.Should().Contain("Version:");
            output.Should().Contain("Commit:");
            output.Should().Contain("Architecture:");
            output.Should().Contain("RID:");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void InfoCommand_HumanReadable_WithList_ShouldIncludeListOutput()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act - include list (may be empty but should show the header)
            InfoCommand.Execute(jsonOutput: false, noList: false);
            var output = sw.ToString();

            // Assert
            output.Should().Contain("dotnetup Information:");
            output.Should().Contain("Installed .NET (managed by dotnetup):");
            output.Should().Contain("Total:");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void InfoCommand_Json_ShouldOutputValidJson()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act - use noList: true to avoid manifest access in unit tests
            InfoCommand.Execute(jsonOutput: true, noList: true);
            var output = sw.ToString();

            // Assert - should be valid JSON
            var jsonAction = () => JsonDocument.Parse(output);
            jsonAction.Should().NotThrow();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void InfoCommand_Json_ShouldContainExpectedProperties()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act - use noList: true to avoid manifest access in unit tests
            InfoCommand.Execute(jsonOutput: true, noList: true);
            var output = sw.ToString();

            // Assert
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            root.TryGetProperty("version", out _).Should().BeTrue();
            root.TryGetProperty("commit", out _).Should().BeTrue();
            root.TryGetProperty("architecture", out _).Should().BeTrue();
            root.TryGetProperty("rid", out _).Should().BeTrue();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void InfoCommand_Json_WithList_ShouldContainInstallationsProperty()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act - include list
            InfoCommand.Execute(jsonOutput: true, noList: false);
            var output = sw.ToString();

            // Assert
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            root.TryGetProperty("installations", out var installations).Should().BeTrue();
            installations.ValueKind.Should().Be(JsonValueKind.Array);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

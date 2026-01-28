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

    [Fact]
    public void Parser_ShouldParseInfoWithJsonOption()
    {
        // Arrange
        var args = new[] { "--info", "--json" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.GetValue(Parser.InfoOption).Should().BeTrue();
        parseResult.GetValue(InfoCommandParser.JsonOption).Should().BeTrue();
    }

    [Fact]
    public void Parser_ShouldParseJsonWithInfoOption()
    {
        // Arrange - test that order doesn't matter
        var args = new[] { "--json", "--info" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.GetValue(Parser.InfoOption).Should().BeTrue();
        parseResult.GetValue(InfoCommandParser.JsonOption).Should().BeTrue();
    }

    [Fact]
    public void InfoCommand_ShouldReturnZeroExitCode()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act
            var exitCode = InfoCommand.Execute(jsonOutput: false);

            // Assert
            exitCode.Should().Be(0);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void InfoCommand_ShouldReturnZeroExitCodeForJson()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act
            var exitCode = InfoCommand.Execute(jsonOutput: true);

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

            // Act
            InfoCommand.Execute(jsonOutput: false);
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
    public void InfoCommand_Json_ShouldOutputValidJson()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act
            InfoCommand.Execute(jsonOutput: true);
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

            // Act
            InfoCommand.Execute(jsonOutput: true);
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
    public void InfoCommand_Json_ArchitectureShouldBeLowercase()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act
            InfoCommand.Execute(jsonOutput: true);
            var output = sw.ToString();

            // Assert
            using var doc = JsonDocument.Parse(output);
            var architecture = doc.RootElement.GetProperty("architecture").GetString();

            architecture.Should().NotBeNull();
            architecture.Should().Be(architecture!.ToLowerInvariant(), "architecture should be lowercase");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void InfoCommand_VersionShouldNotBeEmpty()
    {
        // Arrange - capture and restore stdout
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            // Act
            InfoCommand.Execute(jsonOutput: true);
            var output = sw.ToString();

            // Assert
            using var doc = JsonDocument.Parse(output);
            var version = doc.RootElement.GetProperty("version").GetString();

            version.Should().NotBeNullOrEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Parsing;
using System.Text.Json;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class InfoCommandTests
{
    /// <summary>
    /// Creates an InfoCommand instance with the given parameters.
    /// </summary>
    private static InfoCommand CreateInfoCommand(OutputFormat format, bool noList, TextWriter output)
    {
        // Create a minimal ParseResult for the command
        var parseResult = Parser.Parse(new[] { "--info" });
        return new InfoCommand(parseResult, format, noList, output);
    }

    /// <summary>
    /// Executes the InfoCommand and returns the exit code.
    /// </summary>
    private static int ExecuteInfoCommand(OutputFormat format, bool noList, TextWriter output)
    {
        var command = CreateInfoCommand(format, noList, output);
        return command.Execute();
    }

    [Fact]
    public void Parser_ShouldParseInfoCommand()
    {
        // Arrange - dotnetup --info (like dotnet --info)
        var args = new[] { "--info" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldParseInfoCommandWithJsonOption()
    {
        // Arrange - dotnetup --info --format json
        var args = new[] { "--info", "--format", "json" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(InfoCommandParser.FormatOption).Should().Be(OutputFormat.Json);
    }

    [Fact]
    public void Parser_ShouldParseInfoCommandWithNoListOption()
    {
        // Arrange - dotnetup --info --no-list
        var args = new[] { "--info", "--no-list" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(InfoCommandParser.NoListOption).Should().BeTrue();
    }

    [Theory]
    [InlineData("--format", "json")]
    [InlineData("--format", "text")]
    [InlineData("--no-list", null)]
    public void Parser_InfoOptionsNotAvailableAtRootLevel(string option, string? value)
    {
        // Arrange - try to use --info options without --info (e.g., dotnetup --format json)
        var args = value is null ? new[] { option } : new[] { option, value };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert - should have errors since these options are only on the --info command
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(OutputFormat.Text)]
    [InlineData(OutputFormat.Json)]
    public void InfoCommand_ShouldReturnZeroExitCode(OutputFormat format)
    {
        // Arrange
        using var sw = new StringWriter();

        // Act - use noList: true to avoid manifest access in unit tests
        var exitCode = InfoCommand.Execute(format: format, noList: true, output: sw);

        // Assert
        exitCode.Should().Be(0);
    }

    [Fact]
    public void InfoCommand_HumanReadable_ShouldOutputExpectedFormat()
    {
        // Arrange
        using var sw = new StringWriter();

        // Act - use noList: true to avoid manifest access in unit tests
        InfoCommand.Execute(format: OutputFormat.Text, noList: true, output: sw);
        var output = sw.ToString();

        // Assert
        output.Should().Contain("dotnetup Information:");
        output.Should().Contain("Version:");
        output.Should().Contain("Commit:");
        output.Should().Contain("Architecture:");
        output.Should().Contain("RID:");
    }

    [Fact]
    public void InfoCommand_HumanReadable_WithList_ShouldIncludeListOutput()
    {
        // Arrange
        using var sw = new StringWriter();

        // Act - include list (may be empty but should show the header)
        InfoCommand.Execute(format: OutputFormat.Text, noList: false, output: sw);
        var output = sw.ToString();

        // Assert
        output.Should().Contain("dotnetup Information:");
        output.Should().Contain("Installed .NET (managed by dotnetup):");
        output.Should().Contain("Total:");
    }

    [Fact]
    public void InfoCommand_Json_ShouldOutputValidJson()
    {
        // Arrange
        using var sw = new StringWriter();

        // Act - use noList: true to avoid manifest access in unit tests
        InfoCommand.Execute(format: OutputFormat.Json, noList: true, output: sw);
        var output = sw.ToString();

        // Assert - should be valid JSON
        var jsonAction = () => JsonDocument.Parse(output);
        jsonAction.Should().NotThrow();
    }

    [Fact]
    public void InfoCommand_Json_ShouldContainExpectedProperties()
    {
        // Arrange
        using var sw = new StringWriter();

        // Act - use noList: true to avoid manifest access in unit tests
        InfoCommand.Execute(format: OutputFormat.Json, noList: true, output: sw);
        var output = sw.ToString();

        // Assert
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.TryGetProperty("version", out _).Should().BeTrue();
        root.TryGetProperty("commit", out _).Should().BeTrue();
        root.TryGetProperty("architecture", out _).Should().BeTrue();
        root.TryGetProperty("rid", out _).Should().BeTrue();
    }

    [Fact]
    public void InfoCommand_Json_WithList_ShouldContainInstallationsProperty()
    {
        // Arrange
        using var sw = new StringWriter();

        // Act - include list
        InfoCommand.Execute(format: OutputFormat.Json, noList: false, output: sw);
        var output = sw.ToString();

        // Assert
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.TryGetProperty("installations", out var installations).Should().BeTrue();
        installations.ValueKind.Should().Be(JsonValueKind.Array);
    }
}

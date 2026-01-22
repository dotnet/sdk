// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ParserTests
{
    [Fact]
    public void Parser_ShouldParseValidCommands()
    {
        // Arrange
        var args = new[] { "sdk", "install", "8.0" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldHandleInvalidCommands()
    {
        // Arrange
        var args = new[] { "invalid-command" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parser_ShouldHandleSdkHelp()
    {
        // Arrange
        var args = new[] { "sdk", "--help" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldHandleRootHelp()
    {
        // Arrange
        var args = new[] { "--help" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldParseElevatedAdminPathCommand()
    {
        // Arrange
        var args = new[] { "elevatedadminpath", "removedotnet", @"C:\Users\User\AppData\Local\Temp\dotnetup_elevated\output.txt" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldParseDefaultInstallCommand()
    {
        // Arrange
        var args = new[] { "defaultinstall", "user" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("pwsh")]
    public void Parser_ShouldParseEnvCommandWithValidShell(string shell)
    {
        // Arrange
        var args = new[] { "env", "--shell", shell };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldParseEnvCommandWithCustomPath()
    {
        // Arrange
        var args = new[] { "env", "--shell", "bash", "--dotnet-install-path", "/custom/path" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldParseEnvCommandWithShortOptions()
    {
        // Arrange
        var args = new[] { "env", "-s", "bash", "-d", "/custom/path" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }
}

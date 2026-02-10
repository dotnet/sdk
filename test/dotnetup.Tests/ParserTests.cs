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
        var args = new[] { "print-env-script", "--shell", shell };

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
        var args = new[] { "print-env-script", "--shell", "bash", "--dotnet-install-path", "/custom/path" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }
    [Fact]
    public void Parser_ShouldHandleVersionOption()
    {
        // Arrange
        var args = new[] { "--version" };

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
        var args = new[] { "print-env-script", "-s", "bash", "-d", "/custom/path" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldParseEnvCommandHelp()
    {
        // Arrange
        var args = new[] { "print-env-script", "--help" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_Version_ShouldBeDotnetupVersion()
    {
        // Parser.Version should return the dotnetup assembly version, not any other assembly
        var version = Parser.Version;

        // Should be a valid version format (not "unknown")
        version.Should().NotBe("unknown");
        version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DotnetupProcess_Version_ShouldOutputExpectedVersion()
    {
        // Run dotnetup --version as a process
        var (exitCode, output) = Utilities.DotnetupTestUtilities.RunDotnetupProcess(
            new[] { "--version" },
            captureOutput: true);

        // Should succeed
        exitCode.Should().Be(0);

        // Output should match Parser.Version
        output.Trim().Should().Be(Parser.Version);
    }
}

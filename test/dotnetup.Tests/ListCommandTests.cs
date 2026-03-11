// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ListCommandTests
{
    [Theory]
    [InlineData(new[] { "list" }, OutputFormat.Text, false)]
    [InlineData(new[] { "list", "--format", "json" }, OutputFormat.Json, false)]
    [InlineData(new[] { "list", "--no-verify" }, OutputFormat.Text, true)]
    [InlineData(new[] { "list", "--format", "json", "--no-verify" }, OutputFormat.Json, true)]
    public void Parser_ShouldParseListCommand(string[] args, OutputFormat expectedFormat, bool expectedNoVerify)
    {
        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(CommonOptions.FormatOption).Should().Be(expectedFormat);
        parseResult.GetValue(ListCommandParser.NoVerifyOption).Should().Be(expectedNoVerify);
    }

    [Fact]
    public void InstallationLister_GetInstallations_ShouldReturnList()
    {
        // Act
        var installations = InstallationLister.GetInstallations(verify: false);

        // Assert
        installations.Should().NotBeNull();
        installations.Should().BeOfType<List<InstallationInfo>>();
    }

    [Fact]
    public void InstallationLister_WriteHumanReadable_ShouldOutputHeader()
    {
        // Arrange
        var installations = new List<InstallationInfo>();
        using var sw = new StringWriter();

        // Act
        InstallationLister.WriteHumanReadable(sw, installations);
        var output = sw.ToString();

        // Assert
        output.Should().Contain("Installed .NET (managed by dotnetup):");
        output.Should().Contain("Total: 0");
    }

    [Fact]
    public void InstallationLister_WriteHumanReadable_WithInstallations_ShouldShowDetails()
    {
        // Arrange - use secure temp subdirectory
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-test");
        try
        {
            var testInstallRoot = Path.Combine(tempDir.FullName, ".dotnet");
            var installations = new List<InstallationInfo>
            {
                new() { Component = "sdk", Version = "9.0.100", InstallRoot = testInstallRoot, Architecture = "x64" },
                new() { Component = "runtime", Version = "9.0.0", InstallRoot = testInstallRoot, Architecture = "x64" }
            };
            using var sw = new StringWriter();

            // Act
            InstallationLister.WriteHumanReadable(sw, installations);
            var output = sw.ToString();

            // Assert - should use shorter, punchier display names per @baronfel's suggestion
            output.Should().Contain(".NET SDK");
            output.Should().Contain("9.0.100");
            output.Should().Contain("dotnet (runtime)");
            output.Should().Contain("9.0.0");
            output.Should().Contain(testInstallRoot);
            output.Should().Contain("Total: 2");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void InstallationLister_WriteJson_ShouldOutputValidJson()
    {
        // Arrange
        var installations = new List<InstallationInfo>();
        using var sw = new StringWriter();

        // Act
        InstallationLister.WriteJson(sw, installations);
        var output = sw.ToString();

        // Assert
        var jsonAction = () => JsonDocument.Parse(output);
        jsonAction.Should().NotThrow();
    }

    [Fact]
    public void InstallationLister_WriteJson_ShouldContainExpectedStructure()
    {
        // Arrange - use secure temp subdirectory
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-test");
        try
        {
            var testInstallRoot = tempDir.FullName;
            var installations = new List<InstallationInfo>
            {
                new() { Component = "sdk", Version = "9.0.100", InstallRoot = testInstallRoot, Architecture = "x64" }
            };
            using var sw = new StringWriter();

            // Act
            InstallationLister.WriteJson(sw, installations);
            var output = sw.ToString();

            // Assert
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            root.TryGetProperty("installations", out var installationsArray).Should().BeTrue();
            installationsArray.GetArrayLength().Should().Be(1);

            var firstInstall = installationsArray[0];
            firstInstall.GetProperty("component").GetString().Should().Be("sdk");
            firstInstall.GetProperty("version").GetString().Should().Be("9.0.100");
            firstInstall.GetProperty("installRoot").GetString().Should().Be(testInstallRoot);
            firstInstall.GetProperty("architecture").GetString().Should().Be("x64");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void InstallationLister_WriteJson_EmptyList_ShouldHaveEmptyInstallations()
    {
        // Arrange
        var installations = new List<InstallationInfo>();
        using var sw = new StringWriter();

        // Act
        InstallationLister.WriteJson(sw, installations);
        var output = sw.ToString();

        // Assert
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        root.GetProperty("installations").GetArrayLength().Should().Be(0);
    }
}

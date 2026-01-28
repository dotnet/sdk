// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ListCommandTests
{
    [Theory]
    [InlineData(new[] { "list" }, false, false)]
    [InlineData(new[] { "list", "--json" }, true, false)]
    [InlineData(new[] { "list", "--verify" }, false, true)]
    [InlineData(new[] { "list", "--json", "--verify" }, true, true)]
    public void Parser_ShouldParseListCommand(string[] args, bool expectedJson, bool expectedVerify)
    {
        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(CommonOptions.JsonOption).Should().Be(expectedJson);
        parseResult.GetValue(ListCommandParser.VerifyOption).Should().Be(expectedVerify);
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
        using var tempDir = Directory.CreateTempSubdirectory("dotnetup-test");
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

        // Assert - should use full display names like dotnet --list-runtimes
        output.Should().Contain(".NET SDK");
        output.Should().Contain("9.0.100");
        output.Should().Contain("Microsoft.NETCore.App");
        output.Should().Contain("9.0.0");
        output.Should().Contain(testInstallRoot);
        output.Should().Contain("Total: 2");
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
        using var tempDir = Directory.CreateTempSubdirectory("dotnetup-test");
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
        root.TryGetProperty("total", out var total).Should().BeTrue();
        total.GetInt32().Should().Be(1);

        var firstInstall = installationsArray[0];
        firstInstall.GetProperty("component").GetString().Should().Be("sdk");
        firstInstall.GetProperty("version").GetString().Should().Be("9.0.100");
        firstInstall.GetProperty("installRoot").GetString().Should().Be(testInstallRoot);
        firstInstall.GetProperty("architecture").GetString().Should().Be("x64");
    }

    [Fact]
    public void InstallationLister_WriteJson_EmptyList_ShouldHaveZeroTotal()
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

        root.GetProperty("total").GetInt32().Should().Be(0);
        root.GetProperty("installations").GetArrayLength().Should().Be(0);
    }
}

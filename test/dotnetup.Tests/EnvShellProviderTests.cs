// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class EnvShellProviderTests
{
    [Fact]
    public void BashProvider_ShouldGenerateValidScript()
    {
        // Arrange
        var provider = new BashEnvShellProvider();
        var installPath = "/test/dotnet/path";

        // Act
        var script = provider.GenerateEnvScript(installPath);

        // Assert
        script.Should().NotBeNullOrEmpty();
        script.Should().Contain("#!/usr/bin/env bash");
        script.Should().Contain($"export DOTNET_ROOT='{installPath}'");
        script.Should().Contain($"export PATH='{installPath}':$PATH");
    }

    [Fact]
    public void BashProvider_ShouldIncludeDotnetupDirInPath()
    {
        var provider = new BashEnvShellProvider();
        var script = provider.GenerateEnvScript("/test/dotnet", "/usr/local/bin");

        script.Should().Contain("export PATH='/usr/local/bin':'/test/dotnet':$PATH");
    }

    [Fact]
    public void BashProvider_DotnetupOnly_ShouldNotSetDotnetRoot()
    {
        var provider = new BashEnvShellProvider();
        var script = provider.GenerateEnvScript("/test/dotnet", "/usr/local/bin", includeDotnet: false);

        script.Should().NotContain("DOTNET_ROOT");
        script.Should().Contain("export PATH='/usr/local/bin':$PATH");
        script.Should().NotContain("'/test/dotnet'");
    }

    [Fact]
    public void ZshProvider_ShouldGenerateValidScript()
    {
        // Arrange
        var provider = new ZshEnvShellProvider();
        var installPath = "/test/dotnet/path";

        // Act
        var script = provider.GenerateEnvScript(installPath);

        // Assert
        script.Should().NotBeNullOrEmpty();
        script.Should().Contain("#!/usr/bin/env zsh");
        script.Should().Contain($"export DOTNET_ROOT='{installPath}'");
        script.Should().Contain($"export PATH='{installPath}':$PATH");
    }

    [Fact]
    public void ZshProvider_ShouldIncludeDotnetupDirInPath()
    {
        var provider = new ZshEnvShellProvider();
        var script = provider.GenerateEnvScript("/test/dotnet", "/usr/local/bin");

        script.Should().Contain("export PATH='/usr/local/bin':'/test/dotnet':$PATH");
    }

    [Fact]
    public void ZshProvider_DotnetupOnly_ShouldNotSetDotnetRoot()
    {
        var provider = new ZshEnvShellProvider();
        var script = provider.GenerateEnvScript("/test/dotnet", "/usr/local/bin", includeDotnet: false);

        script.Should().NotContain("DOTNET_ROOT");
        script.Should().Contain("export PATH='/usr/local/bin':$PATH");
    }

    [Fact]
    public void PowerShellProvider_ShouldGenerateValidScript()
    {
        // Arrange
        var provider = new PowerShellEnvShellProvider();
        var installPath = "/test/dotnet/path";

        // Act
        var script = provider.GenerateEnvScript(installPath);

        // Assert
        script.Should().NotBeNullOrEmpty();
        script.Should().Contain($"$env:DOTNET_ROOT = '{installPath}'");
        script.Should().Contain($"$env:PATH = '{installPath}'");
        script.Should().Contain("[IO.Path]::PathSeparator");
    }

    [Fact]
    public void PowerShellProvider_ShouldIncludeDotnetupDirInPath()
    {
        var provider = new PowerShellEnvShellProvider();
        var script = provider.GenerateEnvScript("/test/dotnet", "/usr/local/bin");

        script.Should().Contain("$env:PATH = '/usr/local/bin' + [IO.Path]::PathSeparator + '/test/dotnet' + [IO.Path]::PathSeparator + $env:PATH");
    }

    [Fact]
    public void PowerShellProvider_DotnetupOnly_ShouldNotSetDotnetRoot()
    {
        var provider = new PowerShellEnvShellProvider();
        var script = provider.GenerateEnvScript("/test/dotnet", "/usr/local/bin", includeDotnet: false);

        script.Should().NotContain("DOTNET_ROOT");
        script.Should().Contain("$env:PATH = '/usr/local/bin' + [IO.Path]::PathSeparator + $env:PATH");
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("pwsh")]
    public void ShellProviders_ShouldHaveCorrectArgumentName(string expectedName)
    {
        // Arrange
        var provider = PrintEnvScriptCommandParser.s_supportedShells.FirstOrDefault(s => s.ArgumentName == expectedName);

        // Assert
        provider.Should().NotBeNull();
        provider!.ArgumentName.Should().Be(expectedName);
    }

    [Fact]
    public void BashProvider_ShouldHaveCorrectProperties()
    {
        // Arrange
        var provider = new BashEnvShellProvider();

        // Assert
        provider.ArgumentName.Should().Be("bash");
        provider.Extension.Should().Be("sh");
        provider.HelpDescription.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ZshProvider_ShouldHaveCorrectProperties()
    {
        // Arrange
        var provider = new ZshEnvShellProvider();

        // Assert
        provider.ArgumentName.Should().Be("zsh");
        provider.Extension.Should().Be("zsh");
        provider.HelpDescription.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PowerShellProvider_ShouldHaveCorrectProperties()
    {
        // Arrange
        var provider = new PowerShellEnvShellProvider();

        // Assert
        provider.ArgumentName.Should().Be("pwsh");
        provider.Extension.Should().Be("ps1");
        provider.HelpDescription.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BashProvider_ShouldEscapeSingleQuotesInPath()
    {
        // Arrange
        var provider = new BashEnvShellProvider();
        var installPath = "/test/path/with'quote";

        // Act
        var script = provider.GenerateEnvScript(installPath);

        // Assert
        script.Should().Contain("export DOTNET_ROOT='/test/path/with'\\''quote'");
        script.Should().Contain("export PATH='/test/path/with'\\''quote':$PATH");
    }

    [Fact]
    public void ZshProvider_ShouldEscapeSingleQuotesInPath()
    {
        // Arrange
        var provider = new ZshEnvShellProvider();
        var installPath = "/test/path/with'quote";

        // Act
        var script = provider.GenerateEnvScript(installPath);

        // Assert
        script.Should().Contain("export DOTNET_ROOT='/test/path/with'\\''quote'");
        script.Should().Contain("export PATH='/test/path/with'\\''quote':$PATH");
    }

    [Fact]
    public void PowerShellProvider_ShouldEscapeSingleQuotesInPath()
    {
        // Arrange
        var provider = new PowerShellEnvShellProvider();
        var installPath = "/test/path/with'quote";

        // Act
        var script = provider.GenerateEnvScript(installPath);

        // Assert
        script.Should().Contain("$env:DOTNET_ROOT = '/test/path/with''quote'");
        script.Should().Contain("$env:PATH = '/test/path/with''quote'");
    }
}

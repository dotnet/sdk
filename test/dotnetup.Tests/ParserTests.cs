// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Uninstall;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class ParserTests
{
    public static IEnumerable<object[]> ShellOverrideCommandArgs =>
    [
        [new[] { "defaultinstall", "user", "--shell", "bash" }],
        [new[] { "init", "--shell", "bash" }]
    ];

    public static IEnumerable<object[]> MigrateFromSystemCommandArgs =>
    [
        [new[] { "sdk", "install", "8.0", "--migrate-from-system" }],
        [new[] { "install", "8.0", "--migrate-from-system" }],
        [new[] { "runtime", "install", "aspnetcore@9.0", "--migrate-from-system" }]
    ];

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
    public void Parser_ShouldParseInitCommand()
    {
        var args = new[] { "init", "--help" };

        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(MigrateFromSystemCommandArgs))]
    public void Parser_ShouldParseInstallCommandsWithMigrateFromSystem(string[] args)
    {
        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldRejectWalkthroughCommand()
    {
        var args = new[] { "walkthrough", "--help" };

        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().NotBeEmpty();
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
    [MemberData(nameof(ShellOverrideCommandArgs))]
    public void Parser_ShouldParseCommandsWithShellOverride(string[] args)
    {
        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("sdk", "install", "9.0", "--shell", "zsh")]
    [InlineData("runtime", "install", "aspnetcore@9.0", "--shell", "pwsh")]
    public void Parser_ShouldRejectShellOverrideOnInstallCommands(params string[] args)
    {
        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().NotBeEmpty();
        parseResult.Errors.Select(error => error.Message)
            .Should().Contain(message => message.Contains("--shell"));
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
        // Use AppContext.BaseDirectory as working directory to avoid race conditions
        // with other tests that may delete temp directories
        var (exitCode, output) = Utilities.DotnetupTestUtilities.RunDotnetupProcess(
            new[] { "--version" },
            captureOutput: true,
            workingDirectory: AppContext.BaseDirectory);

        // Should succeed
        exitCode.Should().Be(0);

        // Output should match Parser.Version
        output.Trim().Should().Be(Parser.Version);
    }

    [Fact]
    public void DotnetupProcess_RootHelp_ShouldListInitCommand()
    {
        var (exitCode, output) = Utilities.DotnetupTestUtilities.RunDotnetupProcess(
            new[] { "--help" },
            captureOutput: true,
            workingDirectory: AppContext.BaseDirectory);

        exitCode.Should().Be(0);
        output.Should().Contain("init");
        output.Should().NotContain("migrate");
        output.Should().NotContain("walkthrough");
    }

    #region Runtime Command Parser Tests

    [Theory]
    [InlineData("9.0")]           // Version only - installs core runtime
    [InlineData("latest")]        // Channel - installs core runtime
    [InlineData("aspnetcore@9.0")]
    [InlineData("windowsdesktop@lts")]
    [InlineData("runtime@10.0.1")]
    public void Parser_ShouldParseRuntimeInstallCommand(string componentSpec)
    {
        // Arrange
        var args = new[] { "runtime", "install", componentSpec };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldParseRuntimeInstallWithOptions()
    {
        // Arrange
        var args = new[] { "runtime", "install", "aspnetcore@9.0", "--install-path", @"C:\dotnet", "--no-progress" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldHandleRuntimeHelp()
    {
        // Arrange
        var args = new[] { "runtime", "--help" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldHandleRuntimeInstallHelp()
    {
        // Arrange
        var args = new[] { "runtime", "install", "--help" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_RuntimeInstallAllowsNoArgument()
    {
        // Arrange - no argument is valid (will use default behavior)
        var args = new[] { "runtime", "install" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert - should parse without errors (argument is optional)
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty("component-spec argument is optional");
    }

    [Fact]
    public void Parser_ShouldParseRuntimeInstallWithManifestPath()
    {
        // Arrange
        var args = new[] { "runtime", "install", "9.0", "--manifest-path", @"C:\test\manifest.json" };

        // Act
        var parseResult = Parser.Parse(args);

        // Assert
        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    #endregion

    #region Untracked Option Parser Tests

    [Fact]
    public void Parser_ShouldParseSdkInstallWithUntracked()
    {
        var args = new[] { "sdk", "install", "9.0", "--untracked" };

        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(CommonOptions.UntrackedOption).Should().BeTrue();
    }

    [Fact]
    public void Parser_ShouldParseSdkInstallWithoutUntracked()
    {
        var args = new[] { "sdk", "install", "9.0" };

        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(CommonOptions.UntrackedOption).Should().BeFalse();
    }

    [Fact]
    public void Parser_ShouldParseRuntimeInstallWithUntracked()
    {
        var args = new[] { "runtime", "install", "aspnetcore@9.0", "--untracked" };

        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(CommonOptions.UntrackedOption).Should().BeTrue();
    }

    [Fact]
    public void Parser_ShouldParseRootInstallWithUntracked()
    {
        var args = new[] { "install", "9.0", "--untracked" };

        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
        parseResult.GetValue(CommonOptions.UntrackedOption).Should().BeTrue();
    }

    [Fact]
    public void Parser_BareDotnetup_BindsInteractiveOption()
    {
        // Regression test: bare `dotnetup` routes to SdkInstallCommand, but until
        // CommonOptions.InteractiveOption was registered on the root command,
        // ParseResult.GetValue returned default(bool)=false for the option without
        // running its DefaultValueFactory (!IsCIEnvironmentOrRedirected()). That
        // suppressed first-use onboarding for bare `dotnetup` invocations.
        var parseResult = Parser.Parse([]);

        parseResult.Errors.Should().BeEmpty();
        parseResult.GetResult(CommonOptions.InteractiveOption).Should().NotBeNull(
            "InteractiveOption must be bound on the root command so its DefaultValueFactory runs for bare `dotnetup`");
    }

    #endregion

    #region Channel Argument Parser Tests

    [Theory]
    [InlineData("9.0")]
    [InlineData("latest")]
    [InlineData("10.0.1xx")]
    [InlineData("10.0.100-preview.1.32640")]
    [InlineData("11.0.100-preview.3.26170.106")]
    public void Parser_SdkInstall_PreservesExactChannelArgument(string channel)
    {
        // Regression test: the ChannelArguments Argument<string[]> was previously shared
        // between the `sdk install` and root `install` commands. System.CommandLine may
        // re-parent a shared Argument to the last command that added it, causing the
        // other command's parse result to silently lose the token — leading to global.json
        // fallback and wrong-version installs (e.g. preview.3 → preview.4).
        var args = new[] { "sdk", "install", channel };
        var parseResult = Parser.Parse(args);

        parseResult.Errors.Should().BeEmpty();
        var channels = parseResult.GetValue(SdkInstallCommandParser.SdkChannelArguments);
        channels.Should().NotBeNull();
        channels.Should().ContainSingle().Which.Should().Be(channel);
    }

    [Theory]
    [InlineData("9.0")]
    [InlineData("latest")]
    [InlineData("10.0.100-preview.1.32640")]
    [InlineData("11.0.100-preview.3.26170.106")]
    public void Parser_RootInstall_PreservesExactChannelArgument(string channel)
    {
        // Same regression test for the root `install` command path.
        var args = new[] { "install", channel };
        var parseResult = Parser.Parse(args);

        parseResult.Errors.Should().BeEmpty();
        var channels = parseResult.GetValue(SdkInstallCommandParser.RootChannelArguments);
        channels.Should().NotBeNull();
        channels.Should().ContainSingle().Which.Should().Be(channel);
    }

    [Fact]
    public void Parser_SdkInstall_FullPreviewVersion_WithAllOptions()
    {
        // Regression test: full command line as used by configure-toolset.ps1
        var args = new[] {
            "sdk", "install", "11.0.100-preview.3.26170.106",
            "--install-path", @"D:\sdk\.dotnet",
            "--untracked",
            "--set-default-install", "false",
            "--interactive", "false"
        };
        var parseResult = Parser.Parse(args);

        parseResult.Errors.Should().BeEmpty();
        var channels = parseResult.GetValue(SdkInstallCommandParser.SdkChannelArguments);
        channels.Should().ContainSingle().Which.Should().Be("11.0.100-preview.3.26170.106");
        parseResult.GetValue(CommonOptions.UntrackedOption).Should().BeTrue();
        parseResult.GetValue(CommonOptions.InteractiveOption).Should().BeFalse();
    }

    #endregion

    #region Uninstall Argument Parser Tests

    [Theory]
    [InlineData("9.0")]
    [InlineData("10.0.304")]
    [InlineData("11.0.100-preview.3.26170.106")]
    public void Parser_SdkUninstall_PreservesExactChannelArgument(string channel)
    {
        // Regression: shared Argument<string?> between `sdk uninstall` and root `uninstall`.
        var args = new[] { "sdk", "uninstall", channel };
        var parseResult = Parser.Parse(args);

        parseResult.Errors.Should().BeEmpty();
        var value = parseResult.GetValue(SdkUninstallCommandParser.SdkChannelArgument);
        value.Should().Be(channel);
    }

    [Theory]
    [InlineData("9.0")]
    [InlineData("10.0.304")]
    [InlineData("11.0.100-preview.3.26170.106")]
    public void Parser_RootUninstall_PreservesExactChannelArgument(string channel)
    {
        // Same regression test for the root `uninstall` shortcut.
        var args = new[] { "uninstall", channel };
        var parseResult = Parser.Parse(args);

        parseResult.Errors.Should().BeEmpty();
        var value = parseResult.GetValue(SdkUninstallCommandParser.RootChannelArgument);
        value.Should().Be(channel);
    }

    #endregion

    #region Runtime Argument Parser Tests

    [Theory]
    [InlineData("6.0")]
    [InlineData("aspnetcore@9.0")]
    [InlineData("windowsdesktop@10.0")]
    public void Parser_RuntimeInstall_PreservesExactComponentSpec(string spec)
    {
        var args = new[] { "runtime", "install", spec };
        var parseResult = Parser.Parse(args);

        parseResult.Errors.Should().BeEmpty();
        var specs = parseResult.GetValue(RuntimeInstallCommandParser.ComponentSpecsArgument);
        specs.Should().NotBeNull();
        specs.Should().ContainSingle().Which.Should().Be(spec);
    }

    [Theory]
    [InlineData("6.0")]
    [InlineData("aspnetcore@9.0")]
    [InlineData("windowsdesktop@10.0")]
    public void Parser_RuntimeUninstall_PreservesExactComponentSpec(string spec)
    {
        // Regression: shared Argument<string?> between `runtime uninstall` and root shortcut.
        var args = new[] { "runtime", "uninstall", spec };
        var parseResult = Parser.Parse(args);

        parseResult.Errors.Should().BeEmpty();
        var value = parseResult.GetValue(RuntimeUninstallCommandParser.ComponentSpecArgument);
        value.Should().Be(spec);
    }

    #endregion

    #region Help Differentiation Tests

    [Fact]
    public void Help_RootCommand_ShowsGroupedLayout()
    {
        var (exitCode, output) = Utilities.DotnetupTestUtilities.RunDotnetupProcess(
            new[] { "--help" },
            captureOutput: true,
            workingDirectory: AppContext.BaseDirectory);

        exitCode.Should().Be(0);
        output.Should().Contain("Install Commands:");
        output.Should().Contain("Query Commands:");
        output.Should().Contain("Configuration Commands:");
        output.Should().Contain("Utility Commands:");
    }

    [Theory]
    [InlineData("sdk")]
    [InlineData("runtime")]
    [InlineData("list")]
    public void Help_Subcommand_DoesNotShowGroupedLayout(string subcommand)
    {
        var (exitCode, output) = Utilities.DotnetupTestUtilities.RunDotnetupProcess(
            new[] { subcommand, "--help" },
            captureOutput: true,
            workingDirectory: AppContext.BaseDirectory);

        exitCode.Should().Be(0);
        output.Should().NotContain("Install Commands:");
        output.Should().NotContain("Query Commands:");
        output.Should().NotContain("Configuration Commands:");
        output.Should().NotContain("Utility Commands:");
    }

    [Fact]
    public void Help_SdkSubcommand_ShowsOwnContent()
    {
        var (exitCode, output) = Utilities.DotnetupTestUtilities.RunDotnetupProcess(
            new[] { "sdk", "--help" },
            captureOutput: true,
            workingDirectory: AppContext.BaseDirectory);

        exitCode.Should().Be(0);
        output.Should().Contain("sdk");
        output.Should().Contain("install");
        output.Should().Contain("update");
        output.Should().Contain("uninstall");
    }

    [Fact]
    public void Help_NestedSubcommand_DoesNotShowGroupedLayout()
    {
        var (exitCode, output) = Utilities.DotnetupTestUtilities.RunDotnetupProcess(
            new[] { "sdk", "install", "--help" },
            captureOutput: true,
            workingDirectory: AppContext.BaseDirectory);

        exitCode.Should().Be(0);
        output.Should().NotContain("Install Commands:");
        output.Should().NotContain("Query Commands:");
        output.Should().Contain("install");
    }

    #endregion
}

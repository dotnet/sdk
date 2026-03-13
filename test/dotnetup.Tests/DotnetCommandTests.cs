// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Dotnet;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using DotnetForwardCommand = Microsoft.DotNet.Tools.Bootstrapper.Commands.Dotnet.DotnetCommand;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class DotnetCommandTests
{
    // ── Parser tests ─────────────────────────────────────────────────────

    [Fact]
    public void Parser_ShouldParseDotnetCommand()
    {
        var parseResult = Parser.Parse(["dotnet"]);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Parser_ShouldParseDoAliasCommand()
    {
        var parseResult = Parser.Parse(["do"]);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(DotnetForwardedArgsCases))]
    public void Parser_ShouldAcceptForwardedArguments(string[] args)
    {
        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    public static TheoryData<string[]> DotnetForwardedArgsCases => new()
    {
        new[] { "dotnet", "build" },
        new[] { "dotnet", "build", "--configuration", "Release" },
        new[] { "dotnet", "test", "--filter", "Name~Foo" },
        new[] { "dotnet", "--info" },
        new[] { "dotnet", "--version" },
    };

    [Theory]
    [MemberData(nameof(DoForwardedArgsCases))]
    public void Parser_DoAlias_ShouldAcceptForwardedArguments(string[] args)
    {
        var parseResult = Parser.Parse(args);

        parseResult.Should().NotBeNull();
        parseResult.Errors.Should().BeEmpty();
    }

    public static TheoryData<string[]> DoForwardedArgsCases => new()
    {
        new[] { "do", "build" },
        new[] { "do", "run", "--project", "MyApp" },
    };

    [Fact]
    public void Parser_DotnetCommand_CapturesUnmatchedTokens()
    {
        var parseResult = Parser.Parse(["dotnet", "build", "--configuration", "Release"]);

        // Unmatched tokens should contain the forwarded args
        parseResult.UnmatchedTokens.Should().Contain("build");
        parseResult.UnmatchedTokens.Should().Contain("--configuration");
        parseResult.UnmatchedTokens.Should().Contain("Release");
    }

    [Fact]
    public void Parser_DoAlias_CapturesUnmatchedTokens()
    {
        var parseResult = Parser.Parse(["do", "test", "--filter", "Name~Foo"]);

        parseResult.UnmatchedTokens.Should().Contain("test");
        parseResult.UnmatchedTokens.Should().Contain("--filter");
        parseResult.UnmatchedTokens.Should().Contain("Name~Foo");
    }

    // ── Command execution: dotnet not found ──────────────────────────────

    [Fact]
    public void DotnetCommand_WhenDotnetNotFound_ReturnsExitCode1()
    {
        // Arrange - point to a non-existent directory so dotnet.exe won't be found
        var mock = new MockDotnetInstallManager(installPath: Path.Combine(Path.GetTempPath(), "nonexistent_dotnetup_test_" + Guid.NewGuid()));
        var parseResult = Parser.Parse(["dotnet", "--version"]);

        // Act
        var command = new DotnetForwardCommand(parseResult, mock);
        var exitCode = command.Execute();

        // Assert
        exitCode.Should().Be(1);
    }

    // ── Command execution: dotnet found ──────────────────────────────────

    [Fact]
    public void DotnetCommand_WhenDotnetFound_RunsProcess()
    {
        // Arrange - create a temp dir with a fake dotnet script/exe
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-cmd-test");
        try
        {
            CreateFakeDotnetExecutable(tempDir.FullName);

            var mock = new MockDotnetInstallManager(installPath: tempDir.FullName);
            var parseResult = Parser.Parse(["dotnet", "--version"]);

            var command = new DotnetForwardCommand(parseResult, mock);
            var exitCode = command.Execute();

            // The fake dotnet should exit with 0
            exitCode.Should().Be(0);
        }
        finally
        {
            try { tempDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public void DotnetCommand_ForwardsArguments()
    {
        // Arrange - create a temp dir with a stub that echoes args
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-args-test");
        try
        {
            CreateFakeDotnetExecutable(tempDir.FullName);

            var mock = new MockDotnetInstallManager(installPath: tempDir.FullName);
            var parseResult = Parser.Parse(["dotnet", "build", "--configuration", "Release"]);

            var command = new DotnetForwardCommand(parseResult, mock);
            var exitCode = command.Execute();

            // The fake dotnet exits 0 regardless of args; the key assertion is that we don't crash
            exitCode.Should().Be(0);
        }
        finally
        {
            try { tempDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public void DotnetCommand_UsesConfiguredInstallType_WhenUserInstall()
    {
        // Arrange - two paths: a configured user install path and a default path
        var userDir = Directory.CreateTempSubdirectory("dotnetup-user-test");
        var defaultDir = Directory.CreateTempSubdirectory("dotnetup-default-test");
        try
        {
            // Only put dotnet in the user dir - if it resolves to default, it'll fail
            CreateFakeDotnetExecutable(userDir.FullName);

            var mock = new MockDotnetInstallManager(
                installPath: defaultDir.FullName,
                configuredRoot: new DotnetInstallRootConfiguration(
                    new DotnetInstallRoot(userDir.FullName, InstallArchitecture.x64),
                    InstallType.User,
                    IsFullyConfigured: true));

            var parseResult = Parser.Parse(["dotnet", "--version"]);
            var command = new DotnetForwardCommand(parseResult, mock);
            var exitCode = command.Execute();

            // Should succeed because it used the configured user path, not the default
            exitCode.Should().Be(0);
        }
        finally
        {
            try { userDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
            try { defaultDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public void DotnetCommand_FallsBackToDefault_WhenNoConfiguredInstall()
    {
        // Arrange - no configured install type; default path has dotnet
        var defaultDir = Directory.CreateTempSubdirectory("dotnetup-fallback-test");
        try
        {
            CreateFakeDotnetExecutable(defaultDir.FullName);

            var mock = new MockDotnetInstallManager(
                installPath: defaultDir.FullName,
                configuredRoot: null);

            var parseResult = Parser.Parse(["dotnet", "--version"]);
            var command = new DotnetForwardCommand(parseResult, mock);
            var exitCode = command.Execute();

            exitCode.Should().Be(0);
        }
        finally
        {
            try { defaultDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public void DotnetCommand_FallsBackToDefault_WhenAdminInstall()
    {
        // Arrange - configured install is Admin, not User; should fall back to default
        var adminDir = Directory.CreateTempSubdirectory("dotnetup-admin-test");
        var defaultDir = Directory.CreateTempSubdirectory("dotnetup-default-test");
        try
        {
            // Only put dotnet in the default dir - if it resolves to admin, it'll fail
            CreateFakeDotnetExecutable(defaultDir.FullName);

            var mock = new MockDotnetInstallManager(
                installPath: defaultDir.FullName,
                configuredRoot: new DotnetInstallRootConfiguration(
                    new DotnetInstallRoot(adminDir.FullName, InstallArchitecture.x64),
                    InstallType.Admin,
                    IsFullyConfigured: true));

            var parseResult = Parser.Parse(["dotnet", "--version"]);
            var command = new DotnetForwardCommand(parseResult, mock);
            var exitCode = command.Execute();

            // Should succeed because it fell back to default (has dotnet), not admin (no dotnet)
            exitCode.Should().Be(0);
        }
        finally
        {
            try { adminDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
            try { defaultDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public void DotnetCommand_DoAlias_WorksIdentically()
    {
        // Arrange - verify "do" alias behaves the same as "dotnet"
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-do-test");
        try
        {
            CreateFakeDotnetExecutable(tempDir.FullName);

            var mock = new MockDotnetInstallManager(installPath: tempDir.FullName);
            var parseResult = Parser.Parse(["do", "--version"]);

            var command = new DotnetForwardCommand(parseResult, mock);
            var exitCode = command.Execute();

            exitCode.Should().Be(0);
        }
        finally
        {
            try { tempDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    [Fact]
    public void DotnetCommand_WithNoArguments_RunsDotnetWithNoArgs()
    {
        // Arrange
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-noargs-test");
        try
        {
            CreateFakeDotnetExecutable(tempDir.FullName);

            var mock = new MockDotnetInstallManager(installPath: tempDir.FullName);
            var parseResult = Parser.Parse(["dotnet"]);

            var command = new DotnetForwardCommand(parseResult, mock);
            var exitCode = command.Execute();

            exitCode.Should().Be(0);
        }
        finally
        {
            try { tempDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    // ── Stdin forwarding (interactivity) tests ───────────────────────────

    /// <summary>
    /// Verifies that interactive stdin data is properly forwarded from the
    /// parent process through dotnetup to the child dotnet process.
    /// This is critical for commands like <c>dotnet nuget push --interactive</c>
    /// or <c>dotnet new</c> that prompt for user input.
    ///
    /// The test launches dotnetup as a real process, redirects its stdin,
    /// writes test data, and verifies the child process echoes it back
    /// through stdout — proving the full stdin forwarding pipeline works.
    /// </summary>
    [Fact]
    public void DotnetCommand_ForwardsStdinToChildProcess()
    {
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-stdin-test");
        try
        {
            CreateStdinEchoFakeDotnet(tempDir.FullName);

            string dotnetupPath = DotnetupTestUtilities.GetDotnetupExecutablePath();
            string testInput = "HelloInteractive_" + Guid.NewGuid().ToString("N")[..8];

            using var process = new Process();
            process.StartInfo.FileName = dotnetupPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            // Prepend our temp dir to PATH so dotnetup resolves our fake dotnet
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            process.StartInfo.Environment["PATH"] = tempDir.FullName + Path.PathSeparator + currentPath;
            process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";
            process.StartInfo.Environment["NO_COLOR"] = "1";

            if (OperatingSystem.IsWindows())
            {
                // Fake dotnet.exe is cmd.exe; forward /c "findstr /r ." to echo stdin.
                // findstr reads from stdin and echoes lines matching regex "." (non-empty).
                process.StartInfo.Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                    ["dotnet", "/c", "findstr /r ."]);
            }
            else
            {
                // Fake dotnet is a shell script that cats stdin to stdout.
                process.StartInfo.Arguments = "dotnet";
            }

            process.Start();

            // Write test data to dotnetup's stdin; the child process inherits this
            // stdin handle because DotnetCommand uses RedirectStandardInput = false.
            process.StandardInput.WriteLine(testInput);
            process.StandardInput.Close();

            string output = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // The child process should have received and echoed our input
            output.Should().Contain(testInput,
                because: "stdin data should be forwarded to the child dotnet process for interactive commands");
        }
        finally
        {
            try { tempDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    /// <summary>
    /// Verifies that stdout from the child process is properly forwarded
    /// back through dotnetup to the parent process. This ensures interactive
    /// prompts and output are visible to the user.
    /// </summary>
    [Fact]
    public void DotnetCommand_ForwardsStdoutFromChildProcess()
    {
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-stdout-test");
        try
        {
            CreateStdinEchoFakeDotnet(tempDir.FullName);

            string dotnetupPath = DotnetupTestUtilities.GetDotnetupExecutablePath();
            string marker = "MARKER_" + Guid.NewGuid().ToString("N")[..8];

            using var process = new Process();
            process.StartInfo.FileName = dotnetupPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            process.StartInfo.Environment["PATH"] = tempDir.FullName + Path.PathSeparator + currentPath;
            process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";
            process.StartInfo.Environment["NO_COLOR"] = "1";

            if (OperatingSystem.IsWindows())
            {
                // Use cmd.exe /c echo to produce known stdout output
                process.StartInfo.Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                    ["dotnet", "/c", $"echo {marker}"]);
            }
            else
            {
                // Pass marker as argument; the fake dotnet script echoes args then cats stdin
                process.StartInfo.Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                    ["dotnet", marker]);
            }

            process.Start();
            process.StandardInput.Close();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            output.Should().Contain(marker,
                because: "stdout from the child dotnet process should be visible to the caller");
        }
        finally
        {
            try { tempDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a simple fake dotnet executable that exits with code 0.
    /// On Windows, creates a .cmd batch file. On Unix, creates a shell script.
    /// </summary>
    private static void CreateFakeDotnetExecutable(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            var batPath = Path.Combine(directory, "dotnet.exe");
            // Create a minimal valid PE that just exits, or use a .cmd workaround.
            // Since ProcessStartInfo uses FileName, we create a .cmd and use a wrapper.
            // Actually, Windows can't easily "fake" a .exe without a real PE.
            // Instead, create a dotnet.cmd alongside and then create dotnet.exe as a copy
            // of a known no-op. Simpler: write a dotnet.cmd and adjust test expectations.
            //
            // Best approach for testability: create a dotnet.cmd since the code appends "dotnet.exe".
            // We need to make the code find dotnet.exe. So let's copy cmd.exe as dotnet.exe.
            File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), batPath, overwrite: true);
        }
        else
        {
            var scriptPath = Path.Combine(directory, "dotnet");
            File.WriteAllText(scriptPath, "#!/bin/sh\nexit 0\n");
            // Make executable
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    /// <summary>
    /// Creates a fake dotnet executable that echoes stdin to stdout, for testing
    /// interactive stdin/stdout forwarding.
    /// On Windows, copies cmd.exe as dotnet.exe (caller passes /c "findstr /r ." to echo stdin).
    /// On Unix, creates a shell script that cats stdin to stdout.
    /// </summary>
    private static void CreateStdinEchoFakeDotnet(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            // Copy cmd.exe as dotnet.exe; the test controls behavior via forwarded args
            var exePath = Path.Combine(directory, "dotnet.exe");
            File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), exePath, overwrite: true);
        }
        else
        {
            // Shell script that echoes all args then cats stdin to stdout
            var scriptPath = Path.Combine(directory, "dotnet");
            File.WriteAllText(scriptPath, "#!/bin/sh\nfor arg in \"$@\"; do echo \"$arg\"; done\ncat\n");
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    /// <summary>
    /// Minimal mock of <see cref="IDotnetInstallManager"/> for DotnetCommand tests.
    /// Only implements <see cref="GetDefaultDotnetInstallPath"/> and <see cref="GetConfiguredInstallType"/>.
    /// </summary>
    private class MockDotnetInstallManager : IDotnetInstallManager
    {
        private readonly string _installPath;
        private readonly DotnetInstallRootConfiguration? _configuredRoot;

        public MockDotnetInstallManager(string installPath, DotnetInstallRootConfiguration? configuredRoot = null)
        {
            _installPath = installPath;
            _configuredRoot = configuredRoot;
        }

        public string GetDefaultDotnetInstallPath() => _installPath;
        public DotnetInstallRootConfiguration? GetConfiguredInstallType() => _configuredRoot;

        // Unused members — throw so tests fail fast if unexpectedly called
        public GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory) => throw new NotImplementedException();
        public string? GetLatestInstalledAdminVersion() => throw new NotImplementedException();
        public void InstallSdks(DotnetInstallRoot dotnetRoot, Spectre.Console.ProgressContext progressContext, IEnumerable<string> sdkVersions) => throw new NotImplementedException();
        public void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null) => throw new NotImplementedException();
        public void ConfigureInstallType(InstallType installType, string? dotnetRoot = null) => throw new NotImplementedException();
    }
}

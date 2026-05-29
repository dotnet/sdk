// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Xunit;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
///  Tests for the decision logic in <see cref="NativeEntryPoint.ExecuteCore"/>.
///  Validates the DOTNET_CLI_ENABLEAOT gating, AOT fast path, and fallback behavior.
///  Note: Tests that hit the managed fallback path (ManagedHost.RunApp) will get the
///  "managed fallback not found" error because test env doesn't have dotnet.dll in sdkDir.
/// </summary>
public class NativeEntryPointTests
{
    /// <summary>
    /// Runs a test action with DOTNET_CLI_ENABLEAOT and HOSTFXR_PATH restored afterward.
    /// The xUnit v3 AOT source generator does not support IDisposable on test classes,
    /// so we use a helper with try/finally instead.
    /// </summary>
    private static void WithEnvRestore(Action action)
    {
        string? originalEnableAot = Environment.GetEnvironmentVariable("DOTNET_CLI_ENABLEAOT");
        object? originalHostfxrPath = AppContext.GetData("HOSTFXR_PATH");
        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", originalEnableAot);
            AppContext.SetData("HOSTFXR_PATH", originalHostfxrPath);
        }
    }

    [Fact]
    public void ExecuteCore_AotEnabled_VersionCommand_ReturnsZero()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            int exitCode = NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["--version"]);

            Assert.Equal(0, exitCode);
        });
    }

    [Fact]
    public void ExecuteCore_AotEnabled_InfoCommand_ReturnsZero()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            int exitCode = NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["--info"]);

            Assert.Equal(0, exitCode);
        });
    }

    [Fact]
    public void ExecuteCore_AotEnabled_UnrecognizedCommand_FallsBack()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            // "build" produces parse errors → AOT path skipped → falls to managed fallback
            // Since sdkDir doesn't contain dotnet.dll, this returns 1 (no managed fallback)
            int exitCode = NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["build"]);

            Assert.Equal(1, exitCode);
        });
    }

    [Fact]
    public void ExecuteCore_AotDisabled_VersionCommand_FallsBack()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "false");

            // Even --version falls to managed fallback when AOT is disabled
            int exitCode = NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["--version"]);

            // Returns 1 because managed fallback files don't exist
            Assert.Equal(1, exitCode);
        });
    }

    [Fact]
    public void ExecuteCore_AotNotSet_VersionCommand_FallsBack()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", null);

            int exitCode = NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["--version"]);

            // Default is false → managed fallback → files missing → returns 1
            Assert.Equal(1, exitCode);
        });
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    public void ExecuteCore_AotEnabledVariousFormats_TakesAotPath(string enableValue)
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", enableValue);

            int exitCode = NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["--version"]);

            // All these formats should enable AOT → --version handled → exit 0
            Assert.Equal(0, exitCode);
        });
    }

    [Fact]
    public void ExecuteCore_MissingFallbackFiles_ReturnsOneAndWritesError()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", null);

            var originalErr = Console.Error;
            var stderrWriter = new StringWriter();
            Console.SetError(stderrWriter);

            try
            {
                int exitCode = NativeEntryPoint.ExecuteCore(
                    hostPath: "test-host",
                    dotnetRoot: "test-root",
                    sdkDir: "nonexistent-sdk-dir",
                    hostfxrPath: "",
                    args: ["--version"]);

                Assert.Equal(1, exitCode);
                string stderr = stderrWriter.ToString();
                // Verify the error mentions the expected fallback files
                Assert.Contains("dotnet.dll", stderr);
            }
            finally
            {
                Console.SetError(originalErr);
            }
        });
    }

    [Fact]
    public void ExecuteCore_AotEnabled_UnsupportedCommand_NoAotErrorLeaked()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            var originalErr = Console.Error;
            var stderrWriter = new StringWriter();
            Console.SetError(stderrWriter);

            try
            {
                NativeEntryPoint.ExecuteCore(
                    hostPath: "test-host",
                    dotnetRoot: "test-root",
                    sdkDir: "nonexistent-sdk-dir",
                    hostfxrPath: "",
                    args: ["--list-sdks"]);

                string stderr = stderrWriter.ToString();
                // The only error should be about managed fallback, not AOT parser errors
                Assert.DoesNotContain("Unrecognized", stderr);
                Assert.Contains("dotnet.dll", stderr);
            }
            finally
            {
                Console.SetError(originalErr);
            }
        });
    }

    [Fact]
    public void ExecuteCore_SetsHostfxrPathInAppContext()
    {
        WithEnvRestore(() =>
        {
            string testPath = $"test-hostfxr-{Guid.NewGuid()}";
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: testPath,
                args: ["--version"]);

            Assert.Equal(testPath, AppContext.GetData("HOSTFXR_PATH") as string);
        });
    }

    [Fact]
    public void ExecuteCore_EmptyHostfxrPath_DoesNotSetAppContext()
    {
        WithEnvRestore(() =>
        {
            // Clear any previously set value
            AppContext.SetData("HOSTFXR_PATH", null);
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["--version"]);

            Assert.Null(AppContext.GetData("HOSTFXR_PATH"));
        });
    }
}

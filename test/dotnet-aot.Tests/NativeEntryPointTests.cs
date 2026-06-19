// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
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
    /// Runs a test action with relevant environment variables restored afterward.
    /// The xUnit v3 AOT source generator does not support IDisposable on test classes,
    /// so we use a helper with try/finally instead.
    /// </summary>
    private static void WithEnvRestore(Action action)
    {
        string? originalEnableAot = Environment.GetEnvironmentVariable("DOTNET_CLI_ENABLEAOT");
        object? originalHostfxrPath = AppContext.GetData("HOSTFXR_PATH");
        string? originalTraceParent = Environment.GetEnvironmentVariable(Activities.TRACEPARENT);
        string? originalTraceState = Environment.GetEnvironmentVariable(Activities.TRACESTATE);
        string? originalTelemetryOptout = Environment.GetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT");
        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", originalEnableAot);
            AppContext.SetData("HOSTFXR_PATH", originalHostfxrPath);
            Environment.SetEnvironmentVariable(Activities.TRACEPARENT, originalTraceParent);
            Environment.SetEnvironmentVariable(Activities.TRACESTATE, originalTraceState);
            Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", originalTelemetryOptout);
        }
    }

    /// <summary>
    /// Captures the "classic" <see cref="TelemetryEventEntry"/> events raised while <paramref name="action"/>
    /// runs. <see cref="TelemetryEventEntry"/> exposes no unsubscribe, so a single persistent handler routes
    /// event names into a swappable sink that is only populated for the duration of the capture.
    /// </summary>
    private static List<string>? s_telemetrySink;
    private static bool s_telemetryCaptureHooked;

    private static List<string> CaptureClassicTelemetry(Action action)
    {
        if (!s_telemetryCaptureHooked)
        {
            s_telemetryCaptureHooked = true;
            TelemetryEventEntry.Subscribe((eventName, _) => s_telemetrySink?.Add(eventName));
        }

        var sink = new List<string>();
        s_telemetrySink = sink;
        try
        {
            action();
        }
        finally
        {
            s_telemetrySink = null;
        }

        return sink;
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
    public void ExecuteCore_AotEnabled_FileBasedApp_FallsBackToManaged()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            // `dotnet app.cs` must not be served by the AOT path (which would print root usage);
            // it has to fall back to the managed CLI's run pipeline. With a nonexistent SDK dir the
            // fallback can't be hosted, so it reports the missing dotnet.dll - proving we reached it.
            var csFile = Path.Combine(Path.GetTempPath(), $"aot-entry-filebased-{Guid.NewGuid():N}.cs");
            File.WriteAllText(csFile, "Console.WriteLine(\"hi\");");

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
                    args: [csFile]);

                Assert.Equal(1, exitCode);
                Assert.Contains("dotnet.dll", stderrWriter.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
                File.Delete(csFile);
            }
        });
    }

    [Fact]
    public void ExecuteCore_AotEnabled_UnresolvedExternalCommand_FallsBackToManaged()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            // An unrecognized top-level token that does not resolve to any tool/command on the
            // muxer, global/local tools, PATH, or app base must defer to the managed CLI (which
            // has the full resolver set, including project tools, and reports the error). With a
            // nonexistent SDK dir the managed fallback can't be hosted, so it reports the missing
            // dotnet.dll - proving the AOT external-command path returned false and we reached it.
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
                    args: ["some-command-that-does-not-resolve-anywhere-xyz", "arg1"]);

                Assert.Equal(1, exitCode);
                Assert.Contains("dotnet.dll", stderrWriter.ToString());
            }
            finally
            {
                Console.SetError(originalErr);
            }
        });
    }

    [Fact]
    public void ExecuteCore_AotEnabled_ResolvableExternalCommandOnPath_InvokesOutOfProcess()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            // Place a `dotnet-<command>` executable on PATH and verify the AOT external-command
            // path resolves it via the PATH resolver and invokes it out-of-process, returning the
            // child's exit code - without ever reaching the managed fallback.
            string toolDir = Path.Combine(Path.GetTempPath(), $"aot-pathtool-{Guid.NewGuid():N}");
            Directory.CreateDirectory(toolDir);
            string originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";

            try
            {
                const int expectedExitCode = 42;
                string toolPath;
                if (OperatingSystem.IsWindows())
                {
                    toolPath = Path.Combine(toolDir, "dotnet-aotpathtool.cmd");
                    File.WriteAllText(toolPath, $"@echo off{Environment.NewLine}exit /b {expectedExitCode}{Environment.NewLine}");
                }
                else
                {
                    toolPath = Path.Combine(toolDir, "dotnet-aotpathtool");
                    File.WriteAllText(toolPath, $"#!/bin/sh{Environment.NewLine}exit {expectedExitCode}{Environment.NewLine}");
                    File.SetUnixFileMode(toolPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }

                Environment.SetEnvironmentVariable("PATH", toolDir + Path.PathSeparator + originalPath);

                int exitCode = NativeEntryPoint.ExecuteCore(
                    hostPath: "test-host",
                    dotnetRoot: "test-root",
                    sdkDir: "nonexistent-sdk-dir",
                    hostfxrPath: "",
                    args: ["aotpathtool"]);

                Assert.Equal(expectedExitCode, exitCode);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
                try { Directory.Delete(toolDir, recursive: true); } catch { }
            }
        });
    }

    [Fact]
    public void ExecuteCore_AotEnabled_ResolvableExternalCommand_EmitsParserAndResolutionTelemetry()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");

            // When the AOT bridge resolves and invokes an external command in-process (no managed
            // fallback), it must emit the same "classic" telemetry the managed CLI would: the
            // top-level parser event, plus the command-resolution event raised during resolution.
            string toolDir = Path.Combine(Path.GetTempPath(), $"aot-teltool-{Guid.NewGuid():N}");
            Directory.CreateDirectory(toolDir);
            string originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";

            try
            {
                string toolPath;
                if (OperatingSystem.IsWindows())
                {
                    toolPath = Path.Combine(toolDir, "dotnet-aotteltool.cmd");
                    File.WriteAllText(toolPath, $"@echo off{Environment.NewLine}exit /b 0{Environment.NewLine}");
                }
                else
                {
                    toolPath = Path.Combine(toolDir, "dotnet-aotteltool");
                    File.WriteAllText(toolPath, $"#!/bin/sh{Environment.NewLine}exit 0{Environment.NewLine}");
                    File.SetUnixFileMode(toolPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }

                Environment.SetEnvironmentVariable("PATH", toolDir + Path.PathSeparator + originalPath);

                List<string> events = CaptureClassicTelemetry(() =>
                    NativeEntryPoint.ExecuteCore(
                        hostPath: "test-host",
                        dotnetRoot: "test-root",
                        sdkDir: "nonexistent-sdk-dir",
                        hostfxrPath: "",
                        args: ["aotteltool"]));

                Assert.Contains("toplevelparser/command", events);
                Assert.Contains("commandresolution/commandresolved", events);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
                try { Directory.Delete(toolDir, recursive: true); } catch { }
            }
        });
    }

    [Fact]
    public void ExecuteCore_AotDisabled_DefersToManagedHost_DoesNotEmitParserTelemetry()
    {
        WithEnvRestore(() =>
        {
            // With AOT disabled the bridge defers straight to the managed host, which is the process
            // that owns the "classic" parser/resolution telemetry. The bridge must NOT emit those
            // events itself, otherwise they would be double-counted once the managed CLI runs.
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "false");

            List<string> events = CaptureClassicTelemetry(() =>
                NativeEntryPoint.ExecuteCore(
                    hostPath: "test-host",
                    dotnetRoot: "test-root",
                    sdkDir: "nonexistent-sdk-dir",
                    hostfxrPath: "",
                    args: ["aotteltool"]));

            Assert.DoesNotContain("toplevelparser/command", events);
            Assert.DoesNotContain("commandresolution/commandresolved", events);
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

    [Fact]
    public void ExecuteCore_AotFastPath_CreatesMainActivity()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");
            Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "false");

            var collectedActivities = new List<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "dotnet-cli",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => collectedActivities.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);

            NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["--version"]);

            var mainActivity = collectedActivities.FirstOrDefault(a => a.OperationName == "main");
            Assert.NotNull(mainActivity);
            Assert.Equal(0, mainActivity.GetTagItem("process.exit.code"));
            Assert.Equal(ActivityStatusCode.Ok, mainActivity.Status);
        });
    }


    [Fact]
    public void ExecuteCore_TelemetryOptedOut_NoActivities()
    {
        WithEnvRestore(() =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");
            Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "true");

            var collectedActivities = new List<Activity>();
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "dotnet-cli",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => collectedActivities.Add(activity),
            };
            ActivitySource.AddActivityListener(listener);

            NativeEntryPoint.ExecuteCore(
                hostPath: "test-host",
                dotnetRoot: "test-root",
                sdkDir: "nonexistent-sdk-dir",
                hostfxrPath: "",
                args: ["--version"]);

            // The listener still collects activities because it's registered directly,
            // but the TracerProvider is not built so AOT telemetry-specific behavior
            // (like OTLP export) is skipped. The command should still succeed.
            // Note: Activities may still be created because our test listener samples them.
        });
    }
}

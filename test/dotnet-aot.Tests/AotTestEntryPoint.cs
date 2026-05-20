// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Standalone AOT smoke test entry point. Used when publishing tests as NativeAOT.
// xUnit v3 runner depends on Assembly.Location which is empty in AOT binaries,
// so this bypasses xUnit and directly exercises key test scenarios.
#if PUBLISH_AOT_TESTS

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Tests;

internal class AotTestEntryPoint
{
    private static int _passed;
    private static int _failed;
    private static int _total;

    public static int Main(string[] args)
    {
        Console.WriteLine("=== dotnet-aot NativeAOT Smoke Tests ===");
        Console.WriteLine();

        // AOT Parser tests
        RunTest("Parser: --version parses without errors", () =>
        {
            var result = Parser.Parse(["--version"]);
            Assert(result.Errors.Count == 0, $"Expected no errors, got {result.Errors.Count}");
        });

        RunTest("Parser: --info parses without errors", () =>
        {
            var result = Parser.Parse(["--info"]);
            Assert(result.Errors.Count == 0, $"Expected no errors, got {result.Errors.Count}");
        });

        RunTest("Parser: no args parses without errors", () =>
        {
            var result = Parser.Parse([]);
            Assert(result.Errors.Count == 0, $"Expected no errors, got {result.Errors.Count}");
        });

        RunTest("Parser: unrecognized command has errors", () =>
        {
            var result = Parser.Parse(["build"]);
            Assert(result.Errors.Count > 0, "Expected parse errors for 'build'");
        });

        RunTest("Parser: invoke --version returns 0 and outputs version", () =>
        {
            var parseResult = Parser.Parse(["--version"]);
            var buffered = new BufferedReporter();
            var originalOut = Console.Out;
            var stdoutWriter = new StringWriter();
            Reporter.SetOutput(buffered);
            Console.SetOut(stdoutWriter);
            try
            {
                int exitCode = Parser.Invoke(parseResult);
                Assert(exitCode == 0, $"Expected exit code 0, got {exitCode}");
                string reporterOutput = string.Join("", buffered.Lines);
                string consoleOutput = stdoutWriter.ToString();
                string output = string.IsNullOrEmpty(reporterOutput) ? consoleOutput : reporterOutput;
                Assert(!string.IsNullOrWhiteSpace(output), "Expected version output");
            }
            finally
            {
                Reporter.Reset();
                Console.SetOut(originalOut);
            }
        });

        RunTest("Parser: invoke --info returns 0 and contains expected sections", () =>
        {
            var parseResult = Parser.Parse(["--info"]);
            var buffered = new BufferedReporter();
            var originalOut = Console.Out;
            var stdoutWriter = new StringWriter();
            Reporter.SetOutput(buffered);
            Console.SetOut(stdoutWriter);
            try
            {
                int exitCode = Parser.Invoke(parseResult);
                Assert(exitCode == 0, $"Expected exit code 0, got {exitCode}");
                string reporterOutput = string.Join(Environment.NewLine, buffered.Lines);
                string consoleOutput = stdoutWriter.ToString();
                string output = string.IsNullOrEmpty(reporterOutput) ? consoleOutput : reporterOutput;
                Assert(output.Contains(".NET SDK:"), "Expected '.NET SDK:' in output");
                Assert(output.Contains("Version:"), "Expected 'Version:' in output");
                Assert(output.Contains("Runtime Environment:"), "Expected 'Runtime Environment:' in output");
            }
            finally
            {
                Reporter.Reset();
                Console.SetOut(originalOut);
            }
        });

        // NativeEntryPoint decision logic tests
        RunTest("ExecuteCore: AOT enabled + --version returns 0", () =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");
            try
            {
                int exitCode = NativeEntryPoint.ExecuteCore(
                    "test-host", "test-root", "nonexistent-sdk-dir", "", ["--version"]);
                Assert(exitCode == 0, $"Expected exit code 0, got {exitCode}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", null);
            }
        });

        RunTest("ExecuteCore: AOT disabled + --version falls back (returns 1)", () =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "false");
            try
            {
                int exitCode = NativeEntryPoint.ExecuteCore(
                    "test-host", "test-root", "nonexistent-sdk-dir", "", ["--version"]);
                Assert(exitCode == 1, $"Expected exit code 1 (fallback missing), got {exitCode}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", null);
            }
        });

        RunTest("ExecuteCore: AOT enabled + unrecognized command falls back", () =>
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");
            try
            {
                int exitCode = NativeEntryPoint.ExecuteCore(
                    "test-host", "test-root", "nonexistent-sdk-dir", "", ["build"]);
                Assert(exitCode == 1, $"Expected exit code 1 (fallback missing), got {exitCode}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", null);
            }
        });

        RunTest("ExecuteCore: sets HOSTFXR_PATH in AppContext", () =>
        {
            string testPath = $"test-hostfxr-{Guid.NewGuid()}";
            Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", "true");
            try
            {
                NativeEntryPoint.ExecuteCore(
                    "test-host", "test-root", "nonexistent-sdk-dir", testPath, ["--version"]);
                string? actual = AppContext.GetData("HOSTFXR_PATH") as string;
                Assert(actual == testPath, $"Expected HOSTFXR_PATH='{testPath}', got '{actual}'");
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_CLI_ENABLEAOT", null);
                AppContext.SetData("HOSTFXR_PATH", null);
            }
        });

        // DotnetRootResolver tests
        RunTest("DotnetRootResolver: DOTNET_ROOT env var returns that path", () =>
        {
            bool isWindows = OperatingSystem.IsWindows();
            string dotnetDir = isWindows ? @"C:\dotnet" : "/dotnet";
            string fallback = isWindows
                ? @"C:\fallback" + Path.DirectorySeparatorChar
                : "/fallback" + Path.DirectorySeparatorChar;

            string result = DotnetRootResolver.ResolveDotnetRoot(
                getEnvVar: name => name == "DOTNET_ROOT" ? dotnetDir : null,
                processPath: null,
                processArch: System.Runtime.InteropServices.Architecture.X64,
                isWindows: isWindows,
                directoryExists: _ => true,
                fileExists: _ => false,
                baseDirectory: fallback);

            Assert(result == dotnetDir, $"Expected '{dotnetDir}', got '{result}'");
        });

        RunTest("DotnetRootResolver: walks up from process path", () =>
        {
            bool isWindows = OperatingSystem.IsWindows();
            string dotnetRoot = isWindows ? @"C:\dotnet" : "/dotnet";
            string processPath = Path.Combine(dotnetRoot, "sdk", "11.0.100", isWindows ? "dn.exe" : "dn");
            string dotnetExe = Path.Combine(dotnetRoot, isWindows ? "dotnet.exe" : "dotnet");
            string fallback = (isWindows ? @"C:\fallback" : "/fallback") + Path.DirectorySeparatorChar;

            string result = DotnetRootResolver.ResolveDotnetRoot(
                getEnvVar: _ => null,
                processPath: processPath,
                processArch: System.Runtime.InteropServices.Architecture.X64,
                isWindows: isWindows,
                directoryExists: _ => false,
                fileExists: path => path == dotnetExe,
                baseDirectory: fallback);

            Assert(result == dotnetRoot, $"Expected '{dotnetRoot}', got '{result}'");
        });

        RunTest("DotnetRootResolver: hostfxr picks highest version", () =>
        {
            bool isWindows = OperatingSystem.IsWindows();
            string dotnetRoot = isWindows ? @"C:\dotnet" : "/dotnet";
            string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
            string v800 = Path.Combine(fxrDir, "8.0.0");
            string v901 = Path.Combine(fxrDir, "9.0.1");
            string v900 = Path.Combine(fxrDir, "9.0.0");
            string libName = isWindows ? "hostfxr.dll"
                : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";
            string expectedPath = Path.Combine(v901, libName);

            string result = DotnetRootResolver.ResolveHostfxrPath(
                dotnetRoot: dotnetRoot,
                isWindows: isWindows,
                isMacOS: OperatingSystem.IsMacOS(),
                directoryExists: _ => true,
                getDirectories: _ => new[] { v800, v901, v900 },
                fileExists: _ => true);

            Assert(result == expectedPath, $"Expected '{expectedPath}', got '{result}'");
        });

        // Summary
        Console.WriteLine();
        Console.WriteLine($"=== Results: {_passed} passed, {_failed} failed, {_total} total ===");

        return _failed > 0 ? 1 : 0;
    }

    private static void RunTest(string name, Action test)
    {
        _total++;
        try
        {
            test();
            _passed++;
            Console.WriteLine($"  PASS: {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL: {name}");
            Console.WriteLine($"        {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}

#endif

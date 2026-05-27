// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal sealed class WatchableApp(
        ITestOutputHelper logger,
        string executablePath,
        string commandName,
        IEnumerable<string> commandArguments)
        : IAsyncDisposable
    {
        public static WatchableApp CreateDotnetWatchApp(ITestOutputHelper logger)
            => new(logger, SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath, "watch", ["-bl"]);

        public DebugTestOutputLogger Logger { get; } = new DebugTestOutputLogger(logger);

        public TestFlags TestFlags { get; private set; }

        private AwaitableProcess? _process;

        public AwaitableProcess Process
        {
            get => _process ?? throw new InvalidOperationException("Process has not been started yet.");
        }

        public List<string> WatchArgs { get; } = [.. commandArguments];

        public Dictionary<string, string> EnvironmentVariables { get; } = [];

        public void SuppressVerboseLogging()
        {
            // remove default -bl args
            WatchArgs.Clear();

            // override the default used for testing ("trace"):
            EnvironmentVariables.Add("DOTNET_CLI_CONTEXT_VERBOSE", "");
        }

        public void AssertOutputContains(string message)
            => AssertEx.ContainsSubstring(message, Process.Output);

        public void AssertOutputContains(Regex pattern)
            => AssertEx.ContainsPattern(pattern, Process.Output);

        public void AssertOutputContains(MessageDescriptor descriptor, string? projectDisplay = null)
            => AssertOutputContains(GetPattern(descriptor, projectDisplay, out _));

        public void AssertOutputDoesNotContain(string message)
            => AssertEx.DoesNotContainSubstring(message, Process.Output);

        public void AssertOutputDoesNotContain(Regex pattern)
            => AssertEx.DoesNotContainPattern(pattern, Process.Output);

        public void AssertOutputDoesNotContain(MessageDescriptor descriptor, string? projectDisplay = null)
            => AssertOutputDoesNotContain(GetPattern(descriptor, projectDisplay, out _));

        private static Regex GetPattern(MessageDescriptor descriptor, string? projectDisplay, out string patternDisplay)
        {
            var prefix = projectDisplay != null ? $"[{projectDisplay}] " : "";
            var pattern = new Regex(Regex.Replace(Regex.Escape(prefix + descriptor.Format), @"\\\{[0-9]+\}", ".*"));
            patternDisplay = prefix + descriptor.Format;
            return pattern;
        }

        private void LogFoundOutput(string pattern, string? testPath, int testLine)
            => Logger.Log($"Found output matching: '{pattern}'", testPath, testLine);

        private void LogWaitingForOutput(string pattern, string? testPath, int testLine)
            => Logger.Log($"Waiting for output matching: '{pattern}'", testPath, testLine);

        public async ValueTask<string> WaitUntilOutputContains(string text, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        {
            var matchingLine = Process.Output.FirstOrDefault(line => line.Contains(text));
            if (matchingLine == null)
            {
                LogWaitingForOutput(text, testPath, testLine);
                matchingLine = await Process.GetRequiredOutputLineAsync(line => line.Contains(text));
            }

            LogFoundOutput(text, testPath, testLine);
            return matchingLine;
        }

        public async ValueTask<string> WaitUntilOutputContains(Regex pattern, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        {
            var patternDisplay = pattern.ToString();

            var matchingLine = Process.Output.FirstOrDefault(pattern.IsMatch);
            if (matchingLine == null)
            {
                LogWaitingForOutput(patternDisplay, testPath, testLine);
                matchingLine = await Process.GetRequiredOutputLineAsync(pattern.IsMatch);
            }

            LogFoundOutput(patternDisplay, testPath, testLine);
            return matchingLine;
        }

        public async ValueTask<string> WaitUntilOutputContains(MessageDescriptor descriptor, string? projectDisplay = null, [CallerLineNumber] int testLine = 0, [CallerFilePath] string? testPath = null)
        {
            var pattern = GetPattern(descriptor, projectDisplay, out var patternDisplay);
            var matchingLine = Process.Output.FirstOrDefault(pattern.IsMatch);

            if (matchingLine == null)
            {
                LogWaitingForOutput(patternDisplay, testPath, testLine);
                matchingLine = await Process.GetRequiredOutputLineAsync(line => pattern.IsMatch(line));
            }

            LogFoundOutput(patternDisplay, testPath, testLine);
            return matchingLine;
        }

        public Task<string> WaitForOutputLineContaining(string text, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        {
            LogWaitingForOutput(text, testPath, testLine);
            var line = Process.GetRequiredOutputLineAsync(line => line.Contains(text));
            LogFoundOutput(text, testPath, testLine);
            return line;
        }

        public Task<string> WaitForOutputLineContaining(MessageDescriptor descriptor, string? projectDisplay = null, [CallerLineNumber] int testLine = 0, [CallerFilePath] string? testPath = null)
        {
            var pattern = GetPattern(descriptor, projectDisplay, out var patternDisplay);

            LogWaitingForOutput(patternDisplay, testPath, testLine);
            var line = Process.GetRequiredOutputLineAsync(line => pattern.IsMatch(line));
            LogFoundOutput(patternDisplay, testPath, testLine);

            return line;
        }

        public Task<string> WaitForOutputLineContaining(Regex pattern, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        {
            var patternDisplay = pattern.ToString();

            LogWaitingForOutput(patternDisplay, testPath, testLine);
            var line = Process.GetRequiredOutputLineAsync(line => pattern.IsMatch(line));
            LogFoundOutput(patternDisplay, testPath, testLine);

            return line;
        }

        /// <summary>
        /// Asserts that the watched process outputs a line starting with <paramref name="expectedPrefix"/> and returns the remainder of that line.
        /// </summary>
        public async Task<string> AssertOutputLineStartsWith(string expectedPrefix, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        {
            var display = $"^{expectedPrefix}.*";

            LogWaitingForOutput(display, testPath, testLine);

            var line = await Process.GetOutputLineAsync(line => line.StartsWith(expectedPrefix, StringComparison.Ordinal));

            if (line == null)
            {
                Assert.Fail($"Failed to find expected prefix: '{expectedPrefix}'");
            }
            else
            {
                Assert.StartsWith(expectedPrefix, line, StringComparison.Ordinal);
            }

            var result = line.Substring(expectedPrefix.Length);
            LogFoundOutput(display, testPath, testLine);
            return result;
        }

        public async Task AssertOutputLineEquals(string expectedLine)
            => Assert.Equal("", await AssertOutputLineStartsWith(expectedLine));

        public ProcessStartInfo GetProcessStartInfo(string workingDirectory, string testOutputPath, IEnumerable<string> arguments, TestFlags testFlags)
        {
            var args = new List<string>()
            {
                commandName
            };

            args.AddRange(WatchArgs);
            args.AddRange(arguments);

            var info = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = workingDirectory,
                Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args),
                UseShellExecute = false,
                RedirectStandardInput = false,
            };

            // FileSystemWatcher is unreliable. Use polling for testing to avoid flakiness.
            info.Environment.Add("DOTNET_USE_POLLING_FILE_WATCHER", "true");
            info.Environment.Add("__DOTNET_WATCH_TEST_FLAGS", testFlags.ToString());
            info.Environment.Add("__DOTNET_WATCH_TEST_OUTPUT_DIR", testOutputPath);
            info.Environment.Add("Microsoft_CodeAnalysis_EditAndContinue_LogDir", testOutputPath);
            info.Environment.Add("DOTNET_CLI_CONTEXT_VERBOSE", "trace");

            // Aspire DCP logging:
            info.Environment.Add("DCP_DIAGNOSTICS_LOG_FOLDER", Path.Combine(testOutputPath, "dcp"));
            info.Environment.Add("DCP_DIAGNOSTICS_LOG_LEVEL", "debug");

            // suppress all timeouts:
            info.Environment.Add("DCP_IDE_REQUEST_TIMEOUT_SECONDS", "100000");
            info.Environment.Add("DCP_IDE_NOTIFICATION_TIMEOUT_SECONDS", "100000");
            info.Environment.Add("DCP_IDE_NOTIFICATION_KEEPALIVE_SECONDS", "100000");
            info.Environment.Add("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "1");
            info.Environment.Add("ASPIRE_WATCH_PIPE_CONNECTION_TIMEOUT_SECONDS", "100000");

            // override defaults:
            foreach (var (name, value) in EnvironmentVariables)
            {
                info.Environment[name] = value;
            }

            SdkTestContext.Current.AddTestEnvironmentVariables(info.Environment);

            return info;
        }

        public void Start(
            TestAsset asset,
            IEnumerable<string> arguments,
            string? relativeProjectDirectory = null,
            string? workingDirectory = null,
            TestFlags testFlags = TestFlags.RunningAsTest,
            [CallerFilePath] string? testPath = null,
            [CallerLineNumber] int testLine = 0)
        {
            if (testFlags != TestFlags.None)
            {
                testFlags |= TestFlags.RunningAsTest;
            }

            var projectDirectory = (relativeProjectDirectory != null) ? Path.Combine(asset.Path, relativeProjectDirectory) : asset.Path;

            var testOutputPath = asset.GetWatchTestOutputPath();
            Directory.CreateDirectory(testOutputPath);

            var processStartInfo = GetProcessStartInfo(workingDirectory ?? projectDirectory, testOutputPath, arguments, testFlags);

            _process = new AwaitableProcess(processStartInfo, Logger);

            Logger.Log($"Process {Process.Id} started: '{processStartInfo.FileName} {processStartInfo.Arguments}'", testPath, testLine);

            TestFlags = testFlags;
        }

        public async ValueTask DisposeAsync()
        {
            var process = _process;
            if (process != null)
            {
                await process.DisposeAsync();
            }
        }

        public void SendControlC()
            => SendKey(PhysicalConsole.CtrlC);

        public void SendControlR()
            => SendKey(PhysicalConsole.CtrlR);

        public void SendKey(char c)
        {
            Assert.True(TestFlags.HasFlag(TestFlags.ReadKeyFromStdin));

            Process.Process.StandardInput.Write(c);
            Process.Process.StandardInput.Flush();
        }

        public void UseTestBrowser()
        {
            var path = GetTestBrowserPath();
            EnvironmentVariables.Add("DOTNET_WATCH_BROWSER_PATH", path);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserExecute);
            }
        }

        public static string GetTestBrowserPath()
        {
            var exeExtension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
            return Path.Combine(Path.GetDirectoryName(typeof(WatchableApp).Assembly.Location!)!, "test-browser", "dotnet-watch-test-browser" + exeExtension);
        }
    }
}

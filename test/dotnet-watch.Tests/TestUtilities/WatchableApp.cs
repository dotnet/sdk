// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal sealed class WatchableApp(DebugTestOutputLogger logger) : IDisposable
    {
        // Test apps should output this message as soon as they start running:
        private const string StartedMessage = "Started";

        // Test apps should output this message as soon as they exit:
        private const string ExitingMessage = "Exiting";

        private const string WatchErrorOutputEmoji = "❌";
        private const string WatchFileChanged = "dotnet watch ⌚ File changed:";

        public TestFlags TestFlags { get; private set; }

        public DebugTestOutputLogger Logger => logger;

        public AwaitableProcess Process { get; private set; }

        public List<string> DotnetWatchArgs { get; } = ["--verbose", "-bl"];

        public Dictionary<string, string> EnvironmentVariables { get; } = [];

        public void SuppressVerboseLogging()
        {
            // remove default --verbose and -bl args
            DotnetWatchArgs.Clear();

            // override the default used for testing ("trace"):
            EnvironmentVariables.Add("DOTNET_CLI_CONTEXT_VERBOSE", "");
        }

        public void AssertOutputContains(string message)
            => AssertEx.ContainsSubstring(message, Process.Output);

        public void AssertOutputContains(Regex pattern)
            => AssertEx.ContainsPattern(pattern, Process.Output);

        public void AssertOutputContains(MessageDescriptor descriptor, string projectDisplay = null)
            => AssertOutputContains(GetPattern(descriptor, projectDisplay, out _));

        public void AssertOutputDoesNotContain(string message)
            => AssertEx.DoesNotContainSubstring(message, Process.Output);

        public void AssertOutputDoesNotContain(Regex pattern)
            => AssertEx.DoesNotContainPattern(pattern, Process.Output);

        public void AssertOutputDoesNotContain(MessageDescriptor descriptor, string projectDisplay = null)
            => AssertOutputDoesNotContain(GetPattern(descriptor, projectDisplay, out _));

        private static Regex GetPattern(MessageDescriptor descriptor, string projectDisplay, out string patternDisplay)
        {
            var prefix = projectDisplay != null ? $"[{projectDisplay}] " : "";
            var pattern = new Regex(Regex.Replace(Regex.Escape(prefix + descriptor.Format), @"\\\{[0-9]+\}", ".*"));
            patternDisplay = prefix + descriptor.Format;
            return pattern;
        }

        private void LogFoundOutput(string pattern, string testPath, int testLine)
            => Logger.Log($"Found output matching: '{pattern}'", testPath, testLine);

        private void LogWaitingForOutput(string pattern, string testPath, int testLine)
            => Logger.Log($"Waiting for output matching: '{pattern}'", testPath, testLine);

        public async ValueTask WaitUntilOutputContains(string text, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            if (!Process.Output.Any(line => line.Contains(text)))
            {
                LogWaitingForOutput(text, testPath, testLine);
                _ = await WaitForOutputLineMatching(line => line.Contains(text));
            }

            LogFoundOutput(text, testPath, testLine);
        }

        public async ValueTask WaitUntilOutputContains(Regex pattern, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            var patternDisplay = pattern.ToString();

            if (!Process.Output.Any(line => pattern.IsMatch(line)))
            {
                LogWaitingForOutput(patternDisplay, testPath, testLine);
                _ = await WaitForOutputLineMatching(line => pattern.IsMatch(line));
            }

            LogFoundOutput(patternDisplay, testPath, testLine);
        }

        public async ValueTask WaitUntilOutputContains(MessageDescriptor descriptor, string projectDisplay = null, [CallerLineNumber] int testLine = 0, [CallerFilePath] string testPath = null)
        {
            var pattern = GetPattern(descriptor, projectDisplay, out var patternDisplay);

            if (!Process.Output.Any(line => pattern.IsMatch(line)))
            {
                LogWaitingForOutput(patternDisplay, testPath, testLine);
                _ = await WaitForOutputLineMatching(line => pattern.IsMatch(line));
            }

            LogFoundOutput(patternDisplay, testPath, testLine);
        }

        public Task<string> WaitForOutputLineContaining(string text, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            LogWaitingForOutput(text, testPath, testLine);
            var line = Process.GetOutputLineAsync(success: line => line.Contains(text), failure: _ => false);
            LogFoundOutput(text, testPath, testLine);
            return line;
        }

        public Task<string> WaitForOutputLineContaining(MessageDescriptor descriptor, string projectDisplay = null, [CallerLineNumber] int testLine = 0, [CallerFilePath] string testPath = null)
        {
            var pattern = GetPattern(descriptor, projectDisplay, out var patternDisplay);

            LogWaitingForOutput(patternDisplay, testPath, testLine);
            var line = Process.GetOutputLineAsync(success: line => pattern.IsMatch(line), failure: _ => false);
            LogFoundOutput(patternDisplay, testPath, testLine);

            return line;
        }

        public Task<string> WaitForOutputLineContaining(Regex pattern, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            var patternDisplay = pattern.ToString();

            LogWaitingForOutput(patternDisplay, testPath, testLine);
            var line = Process.GetOutputLineAsync(success: line => pattern.IsMatch(line), failure: _ => false);
            LogFoundOutput(patternDisplay, testPath, testLine);

            return line;
        }

        private Task<string> WaitForOutputLineMatching(Predicate<string> predicate)
            => Process.GetOutputLineAsync(success: predicate, failure: _ => false);

        /// <summary>
        /// Asserts that the watched process outputs a line starting with <paramref name="expectedPrefix"/> and returns the remainder of that line.
        /// </summary>
        public async Task<string> AssertOutputLineStartsWith(string expectedPrefix, Predicate<string> failure = null, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            var display = $"^{expectedPrefix}.*";

            LogWaitingForOutput(display, testPath, testLine);

            var line = await Process.GetOutputLineAsync(
                success: line => line.StartsWith(expectedPrefix, StringComparison.Ordinal),
                failure: failure ?? new Predicate<string>(line => line.Contains(WatchErrorOutputEmoji, StringComparison.Ordinal)));

            if (line == null)
            {
                Assert.Fail(failure != null
                    ? "Encountered failure condition"
                    : $"Failed to find expected prefix: '{expectedPrefix}'");
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

        public Task AssertStarted()
            => AssertOutputLineEquals(StartedMessage);

        public Task AssertFileChanged()
            => AssertOutputLineStartsWith(WatchFileChanged);

        public Task AssertExiting()
            => AssertOutputLineStartsWith(ExitingMessage);

        public void Start(TestAsset asset, IEnumerable<string> arguments, string relativeProjectDirectory = null, string workingDirectory = null, TestFlags testFlags = TestFlags.RunningAsTest)
        {
            if (testFlags != TestFlags.None)
            {
                testFlags |= TestFlags.RunningAsTest;
            }

            var projectDirectory = (relativeProjectDirectory != null) ? Path.Combine(asset.Path, relativeProjectDirectory) : asset.Path;

            var commandSpec = new DotnetCommand(Logger, ["watch", .. DotnetWatchArgs, .. arguments])
            {
                WorkingDirectory = workingDirectory ?? projectDirectory,
            };

            var testOutputPath = asset.GetWatchTestOutputPath();
            Directory.CreateDirectory(testOutputPath);

            // FileSystemWatcher is unreliable. Use polling for testing to avoid flakiness.
            commandSpec.WithEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");

            commandSpec.WithEnvironmentVariable("__DOTNET_WATCH_TEST_FLAGS", testFlags.ToString());
            commandSpec.WithEnvironmentVariable("__DOTNET_WATCH_TEST_OUTPUT_DIR", testOutputPath);
            commandSpec.WithEnvironmentVariable("Microsoft_CodeAnalysis_EditAndContinue_LogDir", testOutputPath);
            commandSpec.WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "trace");

            // suppress all timeouts:
            commandSpec.WithEnvironmentVariable("DCP_IDE_REQUEST_TIMEOUT_SECONDS", "100000");
            commandSpec.WithEnvironmentVariable("DCP_IDE_NOTIFICATION_TIMEOUT_SECONDS", "100000");
            commandSpec.WithEnvironmentVariable("DCP_IDE_NOTIFICATION_KEEPALIVE_SECONDS", "100000");
            commandSpec.WithEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "1");

            // Set up automatic dump collection on uncaught exception for launched processes
            // See https://learn.microsoft.com/en-us/dotnet/core/diagnostics/collect-dumps-crash
            commandSpec.WithEnvironmentVariable("DOTNET_DbgEnableMiniDump", "1");
            commandSpec.WithEnvironmentVariable("DOTNET_DbgMiniDumpType", "2"); // heap dump
            commandSpec.WithEnvironmentVariable("DOTNET_DbgMiniDumpName", Path.Combine(testOutputPath, "%e.%p.%t.dmp")); // <executable>.<pid>.<timestamp>.dmp
            commandSpec.WithEnvironmentVariable("DOTNET_EnableCrashReport", "1");

            foreach (var env in EnvironmentVariables)
            {
                commandSpec.WithEnvironmentVariable(env.Key, env.Value);
            }

            var processStartInfo = commandSpec.GetProcessStartInfo();
            Process = new AwaitableProcess(Logger);
            Process.Start(processStartInfo);

            TestFlags = testFlags;
        }

        public void Dispose()
        {
            Process?.Dispose();
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

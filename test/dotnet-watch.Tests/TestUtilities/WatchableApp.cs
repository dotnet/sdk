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

        public void AssertOutputContains(string message)
            => AssertEx.ContainsSubstring(message, Process.Output);

        public void AssertOutputDoesNotContain(string message)
            => Assert.DoesNotContain(Process.Output, line => line.Contains(message));

        public void AssertOutputContains(Regex pattern)
            => AssertEx.ContainsPattern(pattern, Process.Output);

        public void AssertOutputContains(MessageDescriptor descriptor, string projectDisplay = null)
            => AssertOutputContains(GetPattern(descriptor, projectDisplay));

        private static Regex GetPattern(MessageDescriptor descriptor, string projectDisplay = null)
            => new Regex(Regex.Replace(Regex.Escape((projectDisplay != null ? $"[{projectDisplay}] " : "") + descriptor.Format), @"\\\{[0-9]+\}", ".*"));

        public async ValueTask WaitUntilOutputContains(string text, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            if (Process.Output.Any(line => line.Contains(text)))
            {
                Logger.Log($"Test found output: '{text}'", testPath, testLine);
            }
            else   
            {
                Logger.Log($"Test waiting for output: '{text}'", testPath, testLine);
                _ = await WaitForOutputLineMatching(line => line.Contains(text));
            }
        }

        public async ValueTask WaitUntilOutputContains(Regex pattern, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            if (Process.Output.Any(line => pattern.IsMatch(line)))
            {
                Logger.Log($"Test found output pattern: '{pattern}'", testPath, testLine);
            }
            else
            {
                Logger.Log($"Test waiting for output pattern: '{pattern}'", testPath, testLine);
                _ = await WaitForOutputLineMatching(line => pattern.IsMatch(line));
            }
        }

        public async ValueTask WaitUntilOutputContains(MessageDescriptor descriptor, string projectDisplay = null, [CallerLineNumber] int testLine = 0, [CallerFilePath] string testPath = null)
        {
            var pattern = GetPattern(descriptor, projectDisplay);
            if (Process.Output.Any(line => pattern.IsMatch(line)))
            {
                Logger.Log($"Test found output text format: '{descriptor.Format}'", testPath, testLine);
            }
            else
            {
                Logger.Log($"Test waiting for output text format: '{descriptor.Format}'", testPath, testLine);
                _ = await WaitForOutputLineMatching(line => pattern.IsMatch(line));
            }
        }

        public Task<string> WaitForOutputLineContaining(string text, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            Logger.Log($"Test waiting for output: '{text}'", testPath, testLine);
            return Process.GetOutputLineAsync(success: line => line.Contains(text), failure: _ => false);
        }

        public Task<string> WaitForOutputLineContaining(MessageDescriptor descriptor, string projectDisplay = null, [CallerLineNumber] int testLine = 0, [CallerFilePath] string testPath = null)
        {
            Logger.Log($"Test waiting for text format: '{descriptor.Format}'", testPath, testLine);

            var pattern = GetPattern(descriptor, projectDisplay);
            return Process.GetOutputLineAsync(success: line => pattern.IsMatch(line), failure: _ => false);
        }

        public Task<string> WaitForOutputLineContaining(Regex pattern, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            Logger.Log($"Test waiting for output pattern: '{pattern}'", testPath, testLine);
            return Process.GetOutputLineAsync(success: line => pattern.IsMatch(line), failure: _ => false);
        }

        private Task<string> WaitForOutputLineMatching(Predicate<string> predicate)
            => Process.GetOutputLineAsync(success: predicate, failure: _ => false);

        /// <summary>
        /// Asserts that the watched process outputs a line starting with <paramref name="expectedPrefix"/> and returns the remainder of that line.
        /// </summary>
        public async Task<string> AssertOutputLineStartsWith(string expectedPrefix, Predicate<string> failure = null, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            Logger.Log($"Test waiting for output: '{expectedPrefix}'", testPath, testLine);

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

            return line.Substring(expectedPrefix.Length);
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

            // suppress all timeouts:
            commandSpec.WithEnvironmentVariable("DCP_IDE_REQUEST_TIMEOUT_SECONDS", "100000");
            commandSpec.WithEnvironmentVariable("DCP_IDE_NOTIFICATION_TIMEOUT_SECONDS", "100000");
            commandSpec.WithEnvironmentVariable("DCP_IDE_NOTIFICATION_KEEPALIVE_SECONDS", "100000");
            commandSpec.WithEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "1");

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
    }
}

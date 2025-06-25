﻿// Licensed to the .NET Foundation under one or more agreements.
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

        public List<string> DotnetWatchArgs { get; } = ["--verbose", "/bl:DotnetRun.binlog"];

        public Dictionary<string, string> EnvironmentVariables { get; } = [];

        public bool UsePollingWatcher { get; set; }

        public static string GetLinePrefix(MessageDescriptor descriptor, string projectDisplay = null)
            => $"dotnet watch {descriptor.Emoji}{(projectDisplay != null ? $" [{projectDisplay}]" : "")} {descriptor.Format}";

        public void AssertOutputContains(string message)
            => AssertEx.ContainsSubstring(message, Process.Output);

        public void AssertOutputDoesNotContain(string message)
            => Assert.DoesNotContain(Process.Output, line => line.Contains(message));

        public void AssertOutputContains(Regex pattern)
            => AssertEx.ContainsPattern(pattern, Process.Output);

        public void AssertOutputContains(MessageDescriptor descriptor, string projectDisplay = null)
            => AssertOutputContains(GetLinePrefix(descriptor, projectDisplay));

        public async ValueTask WaitUntilOutputContains(string message, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
        {
            if (!Process.Output.Any(line => line.Contains(message)))
            {
                Logger.Log($"Test waiting for output: '{message}'", testPath, testLine);
                _ = await AssertOutputLine(line => line.Contains(message));
            }
        }

        public Task<string> AssertOutputLineStartsWith(MessageDescriptor descriptor, string projectDisplay = null, Predicate<string> failure = null, [CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
            => AssertOutputLineStartsWith(GetLinePrefix(descriptor, projectDisplay), failure, testPath, testLine);

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

        public Task<string> AssertOutputLine(Predicate<string> predicate)
            => Process.GetOutputLineAsync(success: predicate, failure: _ => false);

        public async Task AssertOutputLineEquals(string expectedLine)
            => Assert.Equal("", await AssertOutputLineStartsWith(expectedLine));

        public Task AssertStarted()
            => AssertOutputLineEquals(StartedMessage);

        /// <summary>
        /// Wait till file watcher starts watching for file changes.
        /// </summary>
        public Task AssertWaitingForChanges([CallerFilePath] string testPath = null, [CallerLineNumber] int testLine = 0)
            => AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges, testPath: testPath, testLine: testLine);

        public async Task AssertWaitingForFileChangeBeforeRestarting()
        {
            // wait for user facing message:
            await AssertOutputLineStartsWith(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            // wait for the file watcher to start watching for changes:
            await AssertWaitingForChanges();
        }

        public Task AssertFileChanged()
            => AssertOutputLineStartsWith(WatchFileChanged);

        public Task AssertExiting()
            => AssertOutputLineStartsWith(ExitingMessage);

        public void Start(TestAsset asset, IEnumerable<string> arguments, string relativeProjectDirectory = null, string workingDirectory = null, TestFlags testFlags = TestFlags.RunningAsTest)
        {
            var projectDirectory = (relativeProjectDirectory != null) ? Path.Combine(asset.Path, relativeProjectDirectory) : asset.Path;

            var commandSpec = new DotnetCommand(Logger, ["watch", .. DotnetWatchArgs, .. arguments])
            {
                WorkingDirectory = workingDirectory ?? projectDirectory,
            };

            var testOutputPath = asset.GetWatchTestOutputPath();
            Directory.CreateDirectory(testOutputPath);
            commandSpec.WithEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
            commandSpec.WithEnvironmentVariable("__DOTNET_WATCH_TEST_FLAGS", testFlags.ToString());
            commandSpec.WithEnvironmentVariable("__DOTNET_WATCH_TEST_OUTPUT_DIR", testOutputPath);
            commandSpec.WithEnvironmentVariable("Microsoft_CodeAnalysis_EditAndContinue_LogDir", testOutputPath);

            // suppress all timeouts:
            commandSpec.WithEnvironmentVariable("DCP_IDE_REQUEST_TIMEOUT_SECONDS", "100000");
            commandSpec.WithEnvironmentVariable("DCP_IDE_NOTIFICATION_TIMEOUT_SECONDS", "100000");
            commandSpec.WithEnvironmentVariable("DCP_IDE_NOTIFICATION_KEEPALIVE_SECONDS", "100000");

            // 0 timeout for process cleanup in tests. We can't send Ctrl+C, so process termination must be forced.
            commandSpec.WithEnvironmentVariable("DOTNET_WATCH_PROCESS_CLEANUP_TIMEOUT_MS", "0");

            commandSpec.WithEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "1");

            foreach (var env in EnvironmentVariables)
            {
                commandSpec.WithEnvironmentVariable(env.Key, env.Value);
            }

            Process = new AwaitableProcess(commandSpec, Logger);
            Process.Start();

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

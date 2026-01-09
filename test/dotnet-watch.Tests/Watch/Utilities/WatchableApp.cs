// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tests
{
    internal sealed class WatchableApp : IDisposable
    {
        // Test apps should output this message as soon as they start running:
        private const string StartedMessage = "Started";

        // Test apps should output this message as soon as they exit:
        private const string ExitingMessage = "Exiting";

        private const string WatchErrorOutputEmoji = "❌";
        private const string WatchFileChanged = "dotnet watch ⌚ File changed:";

        public readonly ITestOutputHelper Logger;
        private bool _prepared;

        public WatchableApp(ITestOutputHelper logger)
        {
            Logger = logger;
        }

        public AwaitableProcess Process { get; private set; }

        public List<string> DotnetWatchArgs { get; } = new() { "--verbose" };

        public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();

        public bool UsePollingWatcher { get; set; }

        public static string GetLinePrefix(MessageDescriptor descriptor)
            => $"dotnet watch {descriptor.Emoji} {descriptor.Format}";

        public Task<string> AssertOutputLineStartsWith(MessageDescriptor descriptor, Predicate<string> failure = null)
            => AssertOutputLineStartsWith(GetLinePrefix(descriptor), failure);

        /// <summary>
        /// Asserts that the watched process outputs a line starting with <paramref name="expectedPrefix"/> and returns the remainder of that line.
        /// </summary>
        public async Task<string> AssertOutputLineStartsWith(string expectedPrefix, Predicate<string> failure = null)
        {
            Logger.WriteLine($"Test waiting for output: '{expectedPrefix}'");

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

        public Task<string> AssertOutputLine(Predicate<string> predicate, Predicate<string> failure = null)
            => Process.GetOutputLineAsync(
                success: predicate,
                failure: failure ?? new Predicate<string>(line => line.Contains(WatchErrorOutputEmoji, StringComparison.Ordinal)));

        public async Task AssertOutputLineEquals(string expectedLine)
            => Assert.Equal("", await AssertOutputLineStartsWith(expectedLine));

        public Task AssertStarted()
            => AssertOutputLineEquals(StartedMessage);

        /// <summary>
        /// Wait till file watcher starts watching for file changes.
        /// </summary>
        public Task AssertWaitingForChanges()
            => AssertOutputLineStartsWith(MessageDescriptor.WaitingForChanges);

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

        private void Prepare(string projectDirectory)
        {
            if (_prepared)
            {
                return;
            }

            var buildCommand = new BuildCommand(Logger, projectDirectory);
            buildCommand.Execute().Should().Pass();

            _prepared = true;
        }

        public void Start(TestAsset asset, IEnumerable<string> arguments, string relativeProjectDirectory = null, string workingDirectory = null, TestFlags testFlags = TestFlags.RunningAsTest)
        {
            var projectDirectory = (relativeProjectDirectory != null) ? Path.Combine(asset.Path, relativeProjectDirectory) : asset.Path;

            Prepare(projectDirectory);

            var commandSpec = new DotnetCommand(Logger, ["watch", .. DotnetWatchArgs, .. arguments])
            {
                WorkingDirectory = workingDirectory ?? projectDirectory,
            };

            commandSpec.WithEnvironmentVariable("HOTRELOAD_DELTA_CLIENT_LOG_MESSAGES", "1");
            commandSpec.WithEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
            commandSpec.WithEnvironmentVariable("__DOTNET_WATCH_TEST_FLAGS", testFlags.ToString());

            var encLogPath = Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT") is { } ciOutputRoot
                ? Path.Combine(ciOutputRoot, ".hotreload", asset.Name)
                : asset.Path + ".hotreload";

            commandSpec.WithEnvironmentVariable("Microsoft_CodeAnalysis_EditAndContinue_LogDir", encLogPath);

            foreach (var env in EnvironmentVariables)
            {
                commandSpec.WithEnvironmentVariable(env.Key, env.Value);
            }

            Process = new AwaitableProcess(commandSpec, Logger);
            Process.Start();
        }

        public void Dispose()
        {
            Process?.Dispose();
        }
    }
}

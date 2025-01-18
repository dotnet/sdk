// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watch
{
    [Flags]
    internal enum TestFlags
    {
        None = 0,
        RunningAsTest = 1 << 0,
        MockBrowser = 1 << 1,

        /// <summary>
        /// Elevates the severity of <see cref="MessageDescriptor.WaitingForChanges"/> from <see cref="MessageSeverity.Output"/>.
        /// </summary>
        ElevateWaitingForChangesMessageSeverity = 1 << 2,

        /// <summary>
        /// Instead of using <see cref="Console.ReadKey()"/> to watch for Ctrl+C, Ctlr+R, and other keys, read from standard input.
        /// This allows tests to trigger key based events.
        /// </summary>
        ReadKeyFromStdin = 1 << 3,
    }

    internal sealed record EnvironmentOptions(
        string WorkingDirectory,
        string MuxerPath,
        TimeSpan ProcessCleanupTimeout,
        bool IsPollingEnabled = false,
        bool SuppressHandlingStaticContentFiles = false,
        bool SuppressMSBuildIncrementalism = false,
        bool SuppressLaunchBrowser = false,
        bool SuppressBrowserRefresh = false,
        bool SuppressEmojis = false,
        TestFlags TestFlags = TestFlags.None,
        string TestOutput = "")
    {
        public static EnvironmentOptions FromEnvironment() => new
        (
            WorkingDirectory: Directory.GetCurrentDirectory(),
            MuxerPath: GetMuxerPathFromEnvironment(),
            ProcessCleanupTimeout: EnvironmentVariables.ProcessCleanupTimeout,
            IsPollingEnabled: EnvironmentVariables.IsPollingEnabled,
            SuppressHandlingStaticContentFiles: EnvironmentVariables.SuppressHandlingStaticContentFiles,
            SuppressMSBuildIncrementalism: EnvironmentVariables.SuppressMSBuildIncrementalism,
            SuppressLaunchBrowser: EnvironmentVariables.SuppressLaunchBrowser,
            SuppressBrowserRefresh: EnvironmentVariables.SuppressBrowserRefresh,
            SuppressEmojis: EnvironmentVariables.SuppressEmojis,
            TestFlags: EnvironmentVariables.TestFlags,
            TestOutput: EnvironmentVariables.TestOutputDir
        );

        public bool RunningAsTest { get => (TestFlags & TestFlags.RunningAsTest) != TestFlags.None; }

        private static string GetMuxerPathFromEnvironment()
        {
            var muxerPath = Environment.ProcessPath;
            Debug.Assert(muxerPath != null);
            Debug.Assert(Path.GetFileNameWithoutExtension(muxerPath) == "dotnet", $"Invalid muxer path {muxerPath}");
            return muxerPath;
        }
    }
}

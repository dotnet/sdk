// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher
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
    }

    internal sealed record EnvironmentOptions(
        string WorkingDirectory,
        string MuxerPath,
        bool IsPollingEnabled = false,
        bool SuppressHandlingStaticContentFiles = false,
        bool SuppressMSBuildIncrementalism = false,
        bool SuppressLaunchBrowser = false,
        bool SuppressBrowserRefresh = false,
        bool SuppressEmojis = false,
        TestFlags TestFlags = TestFlags.None)
    {
        public static EnvironmentOptions FromEnvironment() => new
        (
            WorkingDirectory: Directory.GetCurrentDirectory(),
            MuxerPath: GetMuxerPathFromEnvironment(),
            IsPollingEnabled: EnvironmentVariables.IsPollingEnabled,
            SuppressHandlingStaticContentFiles: EnvironmentVariables.SuppressHandlingStaticContentFiles,
            SuppressMSBuildIncrementalism: EnvironmentVariables.SuppressMSBuildIncrementalism,
            SuppressLaunchBrowser: EnvironmentVariables.SuppressLaunchBrowser,
            SuppressBrowserRefresh: EnvironmentVariables.SuppressBrowserRefresh,
            SuppressEmojis: EnvironmentVariables.SuppressEmojis,
            TestFlags: EnvironmentVariables.TestFlags
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

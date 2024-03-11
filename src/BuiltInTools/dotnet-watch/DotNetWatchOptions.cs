// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher
{
    [Flags]
    internal enum TestFlags
    {
        None = 0,
        RunningAsTest = 1 << 0,
        BrowserRequired = 1 << 1,
    }

    internal sealed record DotNetWatchOptions(
        bool SuppressHandlingStaticContentFiles,
        bool SuppressMSBuildIncrementalism,
        bool SuppressLaunchBrowser,
        bool SuppressBrowserRefresh,
        bool SuppressEmojis,
        TestFlags TestFlags)
    {
        public static DotNetWatchOptions Default { get; } = new DotNetWatchOptions
        (
            SuppressHandlingStaticContentFiles: EnvironmentVariables.SuppressHandlingStaticContentFiles,
            SuppressMSBuildIncrementalism: EnvironmentVariables.SuppressMSBuildIncrementalism,
            SuppressLaunchBrowser: EnvironmentVariables.SuppressLaunchBrowser,
            SuppressBrowserRefresh: EnvironmentVariables.SuppressBrowserRefresh,
            SuppressEmojis: EnvironmentVariables.SuppressEmojis,
            TestFlags: EnvironmentVariables.TestFlags
        );

        public bool NonInteractive { get; set; }
        public bool RunningAsTest { get => (TestFlags & TestFlags.RunningAsTest) != TestFlags.None; }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class DotNetWatchContext
    {
        public required bool HotReloadEnabled { get; init; }
        public required IReporter Reporter { get; init; }
        public required ProcessSpec ProcessSpec { get; init; }
        public required LaunchSettingsProfile LaunchSettingsProfile { get; init; }
        public bool SuppressMSBuildIncrementalism { get; init; }
        public ProjectGraph? ProjectGraph { get; init; }
        public string? TargetFramework { get; init; }
        public IReadOnlyList<(string name, string value)>? BuildProperties { get; init; }

        public FileSet? FileSet { get; set; }
        public FileItem? ChangedFile { get; set; }
        public int Iteration { get; set; } = -1;
        public bool RequiresMSBuildRevaluation { get; set; }
        public BrowserRefreshServer? BrowserRefreshServer { get; set; }
    }
}

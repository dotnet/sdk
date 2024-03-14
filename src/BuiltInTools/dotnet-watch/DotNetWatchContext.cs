// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class DotNetWatchContext
    {
        public required CommandLineOptions Options { get; init; }
        public required EnvironmentOptions EnvironmentOptions { get; init; }
        public required bool HotReloadEnabled { get; init; }
        public required IReporter Reporter { get; init; }
        public required LaunchSettingsProfile LaunchSettingsProfile { get; init; }
        public ProjectGraph? ProjectGraph { get; init; }
    }

    internal sealed class WatchState
    {
        public required ProcessSpec ProcessSpec { get; init; }
        public FileItem? ChangedFile { get; set; }
        public int Iteration { get; set; } = -1;
        public bool RequiresMSBuildRevaluation { get; set; }
        public BrowserRefreshServer? BrowserRefreshServer { get; set; }

        public async ValueTask UpdateBrowserAsync(BrowserConnector browserConnector, ProjectInfo project, CancellationToken cancellationToken)
        {
            if (Iteration == 0)
            {
                ProcessSpec.OnOutput += browserConnector.GetBrowserLaunchTrigger(project, cancellationToken);
                BrowserRefreshServer = await browserConnector.StartRefreshServerAsync(cancellationToken);
                BrowserRefreshServer?.SetEnvironmentVariables(ProcessSpec.EnvironmentVariables);
            }
            else if (BrowserRefreshServer != null)
            {
                // We've detected a change. Notify the browser.
                await BrowserRefreshServer.SendWaitMessageAsync(cancellationToken);
            }
        }

        public void UpdateIterationEnvironment(DotNetWatchContext context)
        {
            ProcessSpec.EnvironmentVariables["DOTNET_WATCH_ITERATION"] = (Iteration + 1).ToString(CultureInfo.InvariantCulture);
            ProcessSpec.EnvironmentVariables["DOTNET_LAUNCH_PROFILE"] = context.LaunchSettingsProfile.LaunchProfileName ?? string.Empty;
        }
    }
}

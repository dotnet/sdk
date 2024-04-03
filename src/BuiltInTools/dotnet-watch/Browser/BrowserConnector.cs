// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed partial class BrowserConnector(DotNetWatchContext context) : IAsyncDisposable
    {
        // This needs to be in sync with the version BrowserRefreshMiddleware is compiled against.
        private static readonly Version s_minimumSupportedVersion = new(6, 0);

        private static readonly Regex s_nowListeningRegex = s_nowListeningOnRegex();

        [GeneratedRegex(@"Now listening on: (?<url>.*)\s*$", RegexOptions.Compiled)]
        private static partial Regex s_nowListeningOnRegex();

        private bool _attemptedBrowserLaunch = false;
        public BrowserRefreshServer? RefreshServer { get; private set; }

        /// <summary>
        /// Get process output handler that will be subscribed to the process output event every time the process is launched.
        /// </summary>
        public DataReceivedEventHandler? GetBrowserLaunchTrigger(ProjectInfo project, CancellationToken cancellationToken)
        {
            if (!CanLaunchBrowser(context, project))
            {
                if (context.EnvironmentOptions.TestFlags.HasFlag(TestFlags.BrowserRequired))
                {
                    context.Reporter.Error("Test requires browser to launch");
                }

                return null;
            }

            return handler;

            void handler(object sender, DataReceivedEventArgs eventArgs)
            {
                // We've redirected the output, but want to ensure that it continues to appear in the user's console.
                Console.WriteLine(eventArgs.Data);

                var match = s_nowListeningRegex.Match(eventArgs.Data ?? "");
                if (!match.Success)
                {
                    return;
                }

                ((Process)sender).OutputDataReceived -= handler;

                if (!_attemptedBrowserLaunch)
                {
                    // first iteration:
                    _attemptedBrowserLaunch = true;
                    LaunchBrowser(context, match.Groups["url"].Value);
                }
                else if (RefreshServer != null)
                {
                    // subsequent iterations (project has been rebuilt and relaunched):
                    context.Reporter.Verbose("Reloading browser.");
                    _ = RefreshServer.ReloadAsync(cancellationToken);
                }
            }
        }

        private static void LaunchBrowser(DotNetWatchContext context, string launchUrl)
        {
            var launchPath = context.LaunchSettingsProfile.LaunchUrl;
            var fileName = Uri.TryCreate(launchPath, UriKind.Absolute, out _) ? launchPath : launchUrl + "/" + launchPath;

            var args = string.Empty;
            if (EnvironmentVariables.BrowserPath is { } browserPath)
            {
                args = fileName;
                fileName = browserPath;
            }

            context.Reporter.Verbose($"Launching browser: {fileName} {args}");

            if (context.EnvironmentOptions.TestFlags != TestFlags.None)
            {
                return;
            }

            var info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = true,
            };

            try
            {
                using var browserProcess = Process.Start(info);
                if (browserProcess is null or { HasExited: true })
                {
                    // dotnet-watch, by default, relies on URL file association to launch browsers. On Windows and MacOS, this works fairly well
                    // where URLs are associated with the default browser. On Linux, this is a bit murky.
                    // From emperical observation, it's noted that failing to launch a browser results in either Process.Start returning a null-value
                    // or for the process to have immediately exited.
                    // We can use this to provide a helpful message.
                    context.Reporter.Output($"Unable to launch the browser. Navigate to {launchUrl}", emoji: "🌐");
                }
            }
            catch (Exception ex)
            {
                context.Reporter.Verbose($"An exception occurred when attempting to launch a browser: {ex}");
            }
        }

        private static bool CanLaunchBrowser(DotNetWatchContext context, ProjectInfo project)
        {
            var reporter = context.Reporter;

            if (context.EnvironmentOptions.SuppressLaunchBrowser)
            {
                return false;
            }

            if (!project.IsNetCoreApp31OrNewer())
            {
                // Browser refresh middleware supports 3.1 or newer
                reporter.Verbose("Browser refresh is only supported in .NET Core 3.1 or newer projects.");
                return false;
            }

            if (context.Options.Command != "run")
            {
                reporter.Verbose("Browser refresh is only supported for run commands.");
                return false;
            }

            if (context.LaunchSettingsProfile is not { LaunchBrowser: true })
            {
                reporter.Verbose("launchSettings does not allow launching browsers.");
                return false;
            }

            reporter.Verbose("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.");
            return true;
        }

        public async ValueTask<BrowserRefreshServer?> StartRefreshServerAsync(CancellationToken cancellationToken)
        {
            if (context.EnvironmentOptions.SuppressBrowserRefresh)
            {
                return null;
            }

            if (context.ProjectGraph is null)
            {
                context.Reporter.Verbose("Unable to determine if this project is a webapp.");
                return null;
            }

            if (!IsBrowserRefreshSupported(context.ProjectGraph))
            {
                context.Reporter.Warn(
                    "Skipping configuring browser-refresh middleware since the target framework version is not supported." +
                    " For more information see 'https://aka.ms/dotnet/watch/unsupported-tfm'.");
                return null;
            }

            if (!IsWebApp(context.ProjectGraph))
            {
                context.Reporter.Verbose("Skipping configuring browser-refresh middleware since this is not a webapp.");
                return null;
            }

            context.Reporter.Verbose("Configuring the app to use browser-refresh middleware.");

            return RefreshServer = await BrowserRefreshServer.CreateAsync(context.EnvironmentOptions, context.Reporter, cancellationToken);
        }

        private static bool IsBrowserRefreshSupported(ProjectGraph context)
        {
            if (context.GraphRoots.FirstOrDefault() is not { } projectNode)
            {
                return false;
            }

            if (projectNode.ProjectInstance.GetPropertyValue("_TargetFrameworkVersionWithoutV") is not string targetFrameworkVersion)
            {
                return false;
            }

            if (!Version.TryParse(targetFrameworkVersion, out var version))
            {
                return false;
            }

            return version >= s_minimumSupportedVersion;
        }

        private static bool IsWebApp(ProjectGraph projectGraph)
        {
            // We only want to enable browser refreshes if this is a WebApp (ASP.NET Core / Blazor app).
            return projectGraph.GraphRoots.FirstOrDefault() is { } projectNode &&
                projectNode.ProjectInstance.GetItems("ProjectCapability").Any(p => p.EvaluatedInclude is "AspNetCore" or "WebAssembly");
        }

        public async ValueTask DisposeAsync()
        {
            if (RefreshServer != null)
            {
                await RefreshServer.DisposeAsync();
            }
        }
    }
}

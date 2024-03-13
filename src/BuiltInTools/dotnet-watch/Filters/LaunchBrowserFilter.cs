// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed partial class LaunchBrowserFilter(DotNetWatchContext context, BrowserRefreshFilter browserRefresh)
    {
        private static readonly Regex s_nowListeningRegex = s_nowListeningOnRegex();

        [GeneratedRegex(@"Now listening on: (?<url>.*)\s*$", RegexOptions.Compiled)]
        private static partial Regex s_nowListeningOnRegex();

        private bool _attemptedBrowserLaunch = false;

        /// <summary>
        /// Get process output handler that will be subscribed to the process output event every time the process is launched.
        /// </summary>
        public DataReceivedEventHandler? GetProcessOutputHandler(ProjectInfo project, CancellationToken cancellationToken)
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
                else if (browserRefresh.Server != null)
                {
                    // subsequent iterations (project has been rebuilt and relaunched):
                    context.Reporter.Verbose("Reloading browser.");
                    _ = browserRefresh.Server.ReloadAsync(cancellationToken);
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
    }
}

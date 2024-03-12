// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class LaunchBrowserFilter(EnvironmentOptions options) : IWatchFilter, IAsyncDisposable
    {
        private static readonly Regex NowListeningRegex = new(@"Now listening on: (?<url>.*)\s*$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromSeconds(10));
        private bool _attemptedBrowserLaunch;
        private Process? _browserProcess;
        private string? _launchPath;
        private CancellationToken _cancellationToken;

        public ValueTask ProcessAsync(DotNetWatchContext context, WatchState state, CancellationToken cancellationToken)
        {
            if (options.SuppressLaunchBrowser)
            {
                return default;
            }

            if (state.Iteration == 0)
            {
                if (CanLaunchBrowser(context, state, out var launchPath))
                {
                    context.Reporter.Verbose("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.");
                    _launchPath = launchPath;
                    _cancellationToken = cancellationToken;

                    // We've redirected the output, but want to ensure that it continues to appear in the user's console.
                    state.ProcessSpec.OnOutput += (_, eventArgs) => Console.WriteLine(eventArgs.Data);
                    state.ProcessSpec.OnOutput += OnOutput;
                }
                else if (options.TestFlags.HasFlag(TestFlags.BrowserRequired))
                {
                    context.Reporter.Error("Test requires browser to launch");
                }
            }

            return default;

            void OnOutput(object sender, DataReceivedEventArgs eventArgs)
            {
                if (string.IsNullOrEmpty(eventArgs.Data))
                {
                    return;
                }

                var match = NowListeningRegex.Match(eventArgs.Data);
                if (match.Success)
                {
                    var launchUrl = match.Groups["url"].Value;

                    var process = (Process)sender;
                    process.OutputDataReceived -= OnOutput;

                    if (!_attemptedBrowserLaunch)
                    {
                        _attemptedBrowserLaunch = true;

                        context.Reporter.Verbose("Launching browser.");

                        try
                        {
                            LaunchBrowser(context, launchUrl);
                        }
                        catch (Exception ex)
                        {
                            context.Reporter.Verbose($"An exception occurred when attempting to launch a browser: {ex}");
                            _browserProcess = null;
                        }

                        if (_browserProcess is null || _browserProcess.HasExited)
                        {
                            // dotnet-watch, by default, relies on URL file association to launch browsers. On Windows and MacOS, this works fairly well
                            // where URLs are associated with the default browser. On Linux, this is a bit murky.
                            // From emperical observation, it's noted that failing to launch a browser results in either Process.Start returning a null-value
                            // or for the process to have immediately exited.
                            // We can use this to provide a helpful message.
                            context.Reporter.Output($"Unable to launch the browser. Navigate to {launchUrl}", emoji: "🌐");
                        }
                    }
                    else if (state.BrowserRefreshServer is { } browserRefresh)
                    {
                        context.Reporter.Verbose("Reloading browser.");
                        _ = browserRefresh.ReloadAsync(_cancellationToken);
                    }
                }
            }
        }

        private void LaunchBrowser(DotNetWatchContext context, string launchUrl)
        {
            var fileName = Uri.TryCreate(_launchPath, UriKind.Absolute, out _) ? _launchPath : launchUrl + "/" + _launchPath;
            var args = string.Empty;
            if (EnvironmentVariables.BrowserPath is { } browserPath)
            {
                args = fileName;
                fileName = browserPath;
            }

            if (options.TestFlags != TestFlags.None)
            {
                context.Reporter.Output($"Launching browser: {fileName} {args}");
                return;
            }

            _browserProcess = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = true,
            });
        }

        private static bool CanLaunchBrowser(DotNetWatchContext context, WatchState state, out string? launchUrl)
        {
            Debug.Assert(state.FileSet?.Project != null);

            launchUrl = null;
            var reporter = context.Reporter;

            if (!state.FileSet.Project.IsNetCoreApp31OrNewer())
            {
                // Browser refresh middleware supports 3.1 or newer
                reporter.Verbose("Browser refresh is only supported in .NET Core 3.1 or newer projects.");
                return false;
            }

            if (!context.HotReloadEnabled)
            {
                var dotnetCommand = state.ProcessSpec.Arguments?.FirstOrDefault();
                if (!string.Equals(dotnetCommand, "run", StringComparison.Ordinal))
                {
                    reporter.Verbose("Browser refresh is only supported for run commands.");
                    return false;
                }
            }

            if (context.LaunchSettingsProfile is not { LaunchBrowser: true })
            {
                reporter.Verbose("launchSettings does not allow launching browsers.");
                return false;
            }

            launchUrl = context.LaunchSettingsProfile.LaunchUrl;
            return true;
        }

        public ValueTask DisposeAsync()
        {
            _browserProcess?.Dispose();
            return default;
        }
    }
}

﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed partial class BrowserConnector(DotNetWatchContext context) : IAsyncDisposable
    {
        // This needs to be in sync with the version BrowserRefreshMiddleware is compiled against.
        private static readonly Version s_minimumSupportedVersion = Versions.Version6_0;

        private static readonly Regex s_nowListeningRegex = s_nowListeningOnRegex();

        [GeneratedRegex(@"Now listening on: (?<url>.*)\s*$", RegexOptions.Compiled)]
        private static partial Regex s_nowListeningOnRegex();

        private readonly object _serversGuard = new();
        private readonly Dictionary<ProjectGraphNode, BrowserRefreshServer?> _servers = [];

        // interlocked
        private ImmutableHashSet<ProjectGraphNode> _browserLaunchAttempted = [];

        public async ValueTask DisposeAsync()
        {
            BrowserRefreshServer?[] serversToDispose;

            lock (_serversGuard)
            {
                serversToDispose = _servers.Values.ToArray();
                _servers.Clear();
            }

            await Task.WhenAll(serversToDispose.Select(async server =>
            {
                if (server != null)
                {
                    await server.DisposeAsync();
                }
            }));
        }

        public async ValueTask<BrowserRefreshServer?> LaunchOrRefreshBrowserAsync(
            ProjectGraphNode projectNode,
            ProcessSpec processSpec,
            EnvironmentVariablesBuilder environmentBuilder,
            ProjectOptions projectOptions,
            CancellationToken cancellationToken)
        {
            // Attach trigger to the process that launches browser on URL found in the process output:
            processSpec.OnOutput += GetBrowserLaunchTrigger(projectNode, projectOptions, cancellationToken);

            BrowserRefreshServer? server;
            bool hasExistingServer;

            lock (_serversGuard)
            {
                hasExistingServer = _servers.TryGetValue(projectNode, out server);
                if (!hasExistingServer)
                {
                    server = IsServerSupported(projectNode) ? new BrowserRefreshServer(context.EnvironmentOptions, context.Reporter) : null;
                    _servers.Add(projectNode, server);
                }
            }

            if (server == null)
            {
                // browser refresh server isn't supported
                return null;
            }

            if (!hasExistingServer)
            {
                // Start the server we just created:
                await server.StartAsync(cancellationToken);
                server.SetEnvironmentVariables(environmentBuilder);
            }
            else
            {
                // Notify the browser of a project rebuild (delta applier notifies of updates):
                await server.SendWaitMessageAsync(cancellationToken);
            }

            return server;
        }

        public bool TryGetRefreshServer(ProjectGraphNode projectNode, [NotNullWhen(true)] out BrowserRefreshServer? server)
        {
            lock (_serversGuard)
            {
                return _servers.TryGetValue(projectNode, out server);
            }
        }

        /// <summary>
        /// Get process output handler that will be subscribed to the process output event every time the process is launched.
        /// </summary>
        public DataReceivedEventHandler? GetBrowserLaunchTrigger(ProjectGraphNode projectNode, ProjectOptions projectOptions, CancellationToken cancellationToken)
        {
            if (!CanLaunchBrowser(context, projectNode, projectOptions, out var launchProfile))
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

                var projectAddedToAttemptedSet = ImmutableInterlocked.Update(ref _browserLaunchAttempted, static (set, projectNode) => set.Add(projectNode), projectNode);
                if (projectAddedToAttemptedSet)
                {
                    // first iteration:
                    LaunchBrowser(launchProfile, match.Groups["url"].Value);
                }
                else if (TryGetRefreshServer(projectNode, out var server))
                {
                    // Subsequent iterations (project has been rebuilt and relaunched).
                    // Use refresh server to reload the browser, if available.
                    context.Reporter.Verbose("Reloading browser.");
                    _ = server.ReloadAsync(cancellationToken);
                }
            }
        }

        private void LaunchBrowser(LaunchSettingsProfile launchProfile, string launchUrl)
        {
            var launchPath = launchProfile.LaunchUrl;
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

        private bool CanLaunchBrowser(DotNetWatchContext context, ProjectGraphNode projectNode, ProjectOptions projectOptions, [NotNullWhen(true)] out LaunchSettingsProfile? launchProfile)
        {
            var reporter = context.Reporter;
            launchProfile = null;

            if (context.EnvironmentOptions.SuppressLaunchBrowser)
            {
                return false;
            }

            if (!projectNode.IsNetCoreApp(minVersion: Versions.Version3_1))
            {
                // Browser refresh middleware supports 3.1 or newer
                reporter.Verbose("Browser refresh is only supported in .NET Core 3.1 or newer projects.");
                return false;
            }

            if (projectOptions.Command != "run")
            {
                reporter.Verbose("Browser refresh is only supported for run commands.");
                return false;
            }

            launchProfile = GetLaunchProfile(projectOptions);
            if (launchProfile is not { LaunchBrowser: true })
            {
                reporter.Verbose("launchSettings does not allow launching browsers.");
                return false;
            }

            reporter.Verbose("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.");
            return true;
        }

        public bool IsServerSupported(ProjectGraphNode projectNode)
        {
            if (context.EnvironmentOptions.SuppressBrowserRefresh)
            {
                return false;
            }

            if (!projectNode.IsNetCoreApp(minVersion: s_minimumSupportedVersion))
            {
                context.Reporter.Warn(
                    "Skipping configuring browser-refresh middleware since the target framework version is not supported." +
                    " For more information see 'https://aka.ms/dotnet/watch/unsupported-tfm'.");

                return false;
            }

            if (!IsWebApp(projectNode))
            {
                context.Reporter.Verbose("Skipping configuring browser-refresh middleware since this is not a webapp.");
                return false;
            }

            context.Reporter.Verbose("Configuring the app to use browser-refresh middleware.");

            return true;
        }

        // We only want to enable browser refresh if this is a WebApp (ASP.NET Core / Blazor app).
        private static bool IsWebApp(ProjectGraphNode projectNode)
            => projectNode.GetCapabilities().Any(value => value is "AspNetCore" or "WebAssembly");

        private LaunchSettingsProfile GetLaunchProfile(ProjectOptions projectOptions)
        {
            var projectDirectory = Path.GetDirectoryName(projectOptions.ProjectPath);
            Debug.Assert(projectDirectory != null);

            return (projectOptions.NoLaunchProfile == true
                ? null : LaunchSettingsProfile.ReadLaunchProfile(projectDirectory, projectOptions.LaunchProfileName, context.Reporter)) ?? new();
        }
    }
}

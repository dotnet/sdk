﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch
{
    internal sealed partial class BrowserConnector(DotNetWatchContext context) : IAsyncDisposable, IStaticAssetChangeApplierProvider
    {
        private readonly record struct ProjectKey(string projectPath, string targetFramework);

        // This needs to be in sync with the version BrowserRefreshMiddleware is compiled against.
        private static readonly Version s_minimumSupportedVersion = Versions.Version6_0;

        private static readonly Regex s_nowListeningRegex = GetNowListeningOnRegex();
        private static readonly Regex s_aspireDashboardUrlRegex = GetAspireDashboardUrlRegex();

        [GeneratedRegex(@"Now listening on: (?<url>.*)\s*$", RegexOptions.Compiled)]
        private static partial Regex GetNowListeningOnRegex();

        [GeneratedRegex(@"Login to the dashboard at (?<url>.*)\s*$", RegexOptions.Compiled)]
        private static partial Regex GetAspireDashboardUrlRegex();

        private readonly Lock _serversGuard = new();
        private readonly Dictionary<ProjectKey, BrowserRefreshServer?> _servers = [];

        // interlocked
        private ImmutableHashSet<ProjectKey> _browserLaunchAttempted = [];

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

        private static ProjectKey GetProjectKey(ProjectGraphNode projectNode)
            => new(projectNode.ProjectInstance.FullPath, projectNode.GetTargetFramework());

        /// <summary>
        /// A single browser refresh server is created for each project that supports browser launching.
        /// When the project is rebuilt we reuse the same refresh server and browser instance.
        /// Reload message is sent to the browser in that case.
        /// </summary>
        public async ValueTask<BrowserRefreshServer?> GetOrCreateBrowserRefreshServerAsync(
            ProjectGraphNode projectNode,
            ProcessSpec processSpec,
            EnvironmentVariablesBuilder environmentBuilder,
            ProjectOptions projectOptions,
            HotReloadAppModel appModel,
            CancellationToken cancellationToken)
        {
            BrowserRefreshServer? server;
            bool hasExistingServer;

            var key = GetProjectKey(projectNode);

            lock (_serversGuard)
            {
                hasExistingServer = _servers.TryGetValue(key, out server);
                if (!hasExistingServer)
                {
                    server = IsServerSupported(projectNode, appModel) ? new BrowserRefreshServer(context.EnvironmentOptions, context.Reporter) : null;
                    _servers.Add(key, server);
                }
            }

            // Attach trigger to the process that detects when the web server reports to the output that it's listening.
            // Launches browser on the URL found in the process output for root projects.
            processSpec.OnOutput += GetBrowserLaunchTrigger(projectNode, projectOptions, server, cancellationToken);

            if (server == null)
            {
                // browser refresh server isn't supported
                return null;
            }

            if (!hasExistingServer)
            {
                // Start the server we just created:
                await server.StartAsync(cancellationToken);
            }

            server.SetEnvironmentVariables(environmentBuilder);

            return server;
        }

        bool IStaticAssetChangeApplierProvider.TryGetApplier(ProjectGraphNode projectNode, [NotNullWhen(true)] out IStaticAssetChangeApplier? applier)
        {
            if (TryGetRefreshServer(projectNode, out var server))
            {
                applier = server;
                return true;
            }

            applier = null;
            return false;
        }

        public bool TryGetRefreshServer(ProjectGraphNode projectNode, [NotNullWhen(true)] out BrowserRefreshServer? server)
        {
            var key = GetProjectKey(projectNode);

            lock (_serversGuard)
            {
                return _servers.TryGetValue(key, out server) && server != null;
            }
        }

        /// <summary>
        /// Get process output handler that will be subscribed to the process output event every time the process is launched.
        /// </summary>
        public Action<OutputLine>? GetBrowserLaunchTrigger(ProjectGraphNode projectNode, ProjectOptions projectOptions, BrowserRefreshServer? server, CancellationToken cancellationToken)
        {
            if (!CanLaunchBrowser(context, projectNode, projectOptions, out var launchProfile))
            {
                if (context.EnvironmentOptions.TestFlags.HasFlag(TestFlags.MockBrowser))
                {
                    context.Reporter.Error("Test requires browser to launch");
                }

                return null;
            }

            bool matchFound = false;

            // Workaround for Aspire dashboard launching: scan for "Login to the dashboard at " prefix in the output and use the URL.
            // TODO: Share launch profile processing logic as implemented in VS with dotnet-run and implement browser launching there.
            var isAspireHost = projectNode.GetCapabilities().Contains(AspireServiceFactory.AppHostProjectCapability);

            return handler;

            void handler(OutputLine line)
            {
                // We've redirected the output, but want to ensure that it continues to appear in the user's console.
                (line.IsError ? Console.Error : Console.Out).WriteLine(line.Content);

                if (matchFound)
                {
                    return;
                }

                var match = (isAspireHost ? s_aspireDashboardUrlRegex : s_nowListeningRegex).Match(line.Content);
                if (!match.Success)
                {
                    return;
                }

                matchFound = true;

                if (projectOptions.IsRootProject &&
                    ImmutableInterlocked.Update(ref _browserLaunchAttempted, static (set, key) => set.Add(key), GetProjectKey(projectNode)))
                {
                    // first build iteration of a root project:
                    var launchUrl = GetLaunchUrl(launchProfile.LaunchUrl, match.Groups["url"].Value);
                    LaunchBrowser(launchUrl, server);
                }
                else if (server != null)
                {
                    // Subsequent iterations (project has been rebuilt and relaunched).
                    // Use refresh server to reload the browser, if available.
                    context.Reporter.Verbose("Reloading browser.");
                    _ = server.SendReloadMessageAsync(cancellationToken);
                }
            }
        }

        public static string GetLaunchUrl(string? profileLaunchUrl, string outputLaunchUrl)
            => string.IsNullOrWhiteSpace(profileLaunchUrl) ? outputLaunchUrl :
                Uri.TryCreate(profileLaunchUrl, UriKind.Absolute, out _) ? profileLaunchUrl :
                Uri.TryCreate(outputLaunchUrl, UriKind.Absolute, out var launchUri) ? new Uri(launchUri, profileLaunchUrl).ToString() :
                outputLaunchUrl;

        private void LaunchBrowser(string launchUrl, BrowserRefreshServer? server)
        {
            var fileName = launchUrl;

            var args = string.Empty;
            if (EnvironmentVariables.BrowserPath is { } browserPath)
            {
                args = fileName;
                fileName = browserPath;
            }

            context.Reporter.Verbose($"Launching browser: {fileName} {args}");

            if (context.EnvironmentOptions.TestFlags != TestFlags.None)
            {
                if (context.EnvironmentOptions.TestFlags.HasFlag(TestFlags.MockBrowser))
                {
                    Debug.Assert(server != null);
                    server.EmulateClientConnected();
                }

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

            if (!CommandLineOptions.IsCodeExecutionCommand(projectOptions.Command))
            {
                reporter.Verbose($"Command '{projectOptions.Command}' does not support browser refresh.");
                return false;
            }

            launchProfile = GetLaunchProfile(projectOptions);
            if (launchProfile is not { LaunchBrowser: true })
            {
                reporter.Verbose("launchSettings does not allow launching browsers.");
                return false;
            }

            reporter.Report(MessageDescriptor.ConfiguredToLaunchBrowser);
            return true;
        }

        public bool IsServerSupported(ProjectGraphNode projectNode, HotReloadAppModel appModel)
        {
            if (context.EnvironmentOptions.SuppressBrowserRefresh)
            {
                context.Reporter.Report(MessageDescriptor.SkippingConfiguringBrowserRefresh_SuppressedViaEnvironmentVariable.ToErrorWhen(appModel.RequiresBrowserRefresh), EnvironmentVariables.SuppressBrowserRefresh);
                return false;
            }

            if (!projectNode.IsNetCoreApp(minVersion: s_minimumSupportedVersion))
            {
                context.Reporter.Report(MessageDescriptor.SkippingConfiguringBrowserRefresh_TargetFrameworkNotSupported.ToErrorWhen(appModel.RequiresBrowserRefresh));
                return false;
            }

            // We only want to enable browser refresh if this is a WebApp (ASP.NET Core / Blazor app).
            if (!projectNode.IsWebApp())
            {
                context.Reporter.Report(MessageDescriptor.SkippingConfiguringBrowserRefresh_NotWebApp.ToErrorWhen(appModel.RequiresBrowserRefresh));
                return false;
            }

            context.Reporter.Report(MessageDescriptor.ConfiguredToUseBrowserRefresh);
            return true;
        }

        private LaunchSettingsProfile GetLaunchProfile(ProjectOptions projectOptions)
        {
            return (projectOptions.NoLaunchProfile == true
                ? null : LaunchSettingsProfile.ReadLaunchProfile(projectOptions.ProjectPath, projectOptions.LaunchProfileName, context.Reporter)) ?? new();
        }
    }
}

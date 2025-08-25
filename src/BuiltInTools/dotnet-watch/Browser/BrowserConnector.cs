// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed partial class BrowserConnector(DotNetWatchContext context) : IAsyncDisposable
{
    private readonly record struct ProjectKey(string projectPath, string targetFramework);

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
                server = TryCreateRefreshServer(projectNode, appModel);
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

    private BrowserRefreshServer? TryCreateRefreshServer(ProjectGraphNode projectNode, HotReloadAppModel appModel)
    {
        var logger = context.LoggerFactory.CreateLogger(BrowserRefreshServer.ServerLogComponentName, projectNode.GetDisplayName());

        if (appModel is WebApplicationAppModel webApp && webApp.IsServerSupported(projectNode, context.EnvironmentOptions, logger))
        {
            return new BrowserRefreshServer(context.EnvironmentOptions, logger, context.LoggerFactory);
        }

        return null;
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
                context.Logger.LogError("Test requires browser to launch");
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

        context.Logger.LogDebug("Launching browser: {FileName} {Args}", fileName, args);

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
                context.Logger.LogInformation("Unable to launch the browser. Url '{Url}'.", launchUrl);
            }
        }
        catch (Exception e)
        {
            context.Logger.LogDebug("Failed to launch a browser: {Message}", e.Message);
        }
    }

    private bool CanLaunchBrowser(DotNetWatchContext context, ProjectGraphNode projectNode, ProjectOptions projectOptions, [NotNullWhen(true)] out LaunchSettingsProfile? launchProfile)
    {
        var logger = context.Logger;
        launchProfile = null;

        if (context.EnvironmentOptions.SuppressLaunchBrowser)
        {
            return false;
        }

        if (!projectNode.IsNetCoreApp(minVersion: Versions.Version3_1))
        {
            // Browser refresh middleware supports 3.1 or newer
            logger.LogDebug("Browser refresh is only supported in .NET Core 3.1 or newer projects.");
            return false;
        }

        if (!CommandLineOptions.IsCodeExecutionCommand(projectOptions.Command))
        {
            logger.LogDebug("Command '{Command}' does not support browser refresh.", projectOptions.Command);
            return false;
        }

        launchProfile = GetLaunchProfile(projectOptions);
        if (launchProfile is not { LaunchBrowser: true })
        {
            logger.LogDebug("launchSettings does not allow launching browsers.");
            return false;
        }

        logger.Log(MessageDescriptor.ConfiguredToLaunchBrowser);
        return true;
    }

    private LaunchSettingsProfile GetLaunchProfile(ProjectOptions projectOptions)
    {
        return (projectOptions.NoLaunchProfile == true
            ? null : LaunchSettingsProfile.ReadLaunchProfile(projectOptions.ProjectPath, projectOptions.LaunchProfileName, context.Logger)) ?? new();
    }
}

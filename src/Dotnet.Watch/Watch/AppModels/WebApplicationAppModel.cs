// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Graph;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal abstract class WebApplicationAppModel(DotNetWatchContext context) : HotReloadAppModel
{
    public const string ServerLogComponentName = "BrowserRefreshServer";
    public const string ConnectionServerLogComponentName = "BrowserConnection:Server";
    public const string ConnectionAgentLogComponentName = "BrowserConnection:Agent";

    // This needs to be in sync with the version the browser tools hosting startup is compiled against.
    private static readonly Version s_minimumSupportedVersion = Versions.Version6_0;
    private const string MiddlewareTargetFramework = "net6.0";

    public DotNetWatchContext Context => context;

    public abstract bool ManagedHotReloadRequiresBrowserRefresh { get; }

    /// <summary>
    /// Project that's used for launching the application.
    /// </summary>
    public abstract ProjectGraphNode LaunchingProject { get; }

    /// <summary>
    /// True if gateway proxy is used to serve the app.
    /// </summary>
    public abstract bool HasGatewayProxy { get; }

    /// <summary>
    /// Project whose browser initializer, settings document and pinned public key are served to the
    /// browser. Usually the launching project; for hosted WebAssembly it is the client project.
    /// </summary>
    public virtual ProjectGraphNode BrowserToolsProject => LaunchingProject;

    protected abstract ImmutableArray<HotReloadClient> CreateManagedClients(ILogger clientLogger, ILogger agentLogger, BrowserRefreshServer? browserRefreshServer);

    public async sealed override ValueTask<HotReloadClients> CreateClientsAsync(ILogger clientLogger, ILogger agentLogger, CancellationToken cancellationToken)
    {
        var browserRefreshServer = await context.BrowserRefreshServerFactory.GetOrCreateBrowserRefreshServerAsync(LaunchingProject, this, cancellationToken);

        var managedClients = (!ManagedHotReloadRequiresBrowserRefresh || browserRefreshServer != null) && IsManagedAgentSupported(LaunchingProject, clientLogger)
            ? CreateManagedClients(clientLogger, agentLogger, browserRefreshServer)
            : [];

        return new HotReloadClients(managedClients, browserRefreshServer, useRefreshServerToApplyStaticAssets: true);
    }

    protected WebAssemblyHotReloadClient CreateWebAssemblyClient(ILogger clientLogger, ILogger agentLogger, BrowserRefreshServer browserRefreshServer, ProjectGraphNode clientProject)
    {
        var capabilities = clientProject.GetWebAssemblyCapabilities().ToImmutableArray();
        var targetFramework = clientProject.GetTargetFrameworkVersion() ?? throw new InvalidOperationException($"Project doesn't define {PropertyNames.TargetFrameworkMoniker}");

        return new WebAssemblyHotReloadClient(clientLogger, agentLogger, browserRefreshServer, browserRefreshServer.ResetUpdates(), capabilities, targetFramework, context.EnvironmentOptions.TestFlags.HasFlag(TestFlags.MockBrowser));
    }

    private static string GetMiddlewareAssemblyPath()
        => GetInjectedAssemblyPath(MiddlewareTargetFramework, "Microsoft.AspNetCore.Watch.BrowserRefresh");

    /// <summary>
    /// Creates the browser tools provider for the project. The application host is configured by
    /// <see cref="ConfigureBrowserToolsLaunchEnvironment"/> to expose it on the app's own origin.
    ///
    /// Each connection is authenticated with the private half of the key pair the project's build
    /// produced. The key is loaded when the browser connects, after <c>dotnet run</c> has built and
    /// launched the application that serves the matching public half.
    /// </summary>
    public BrowserRefreshServer? TryCreateRefreshServer(ProjectGraphNode projectNode)
    {
        var logger = context.LoggerFactory.CreateLogger(ServerLogComponentName, projectNode.GetDisplayName());

        if (!IsServerSupported(projectNode, logger))
        {
            return null;
        }

        if (BrowserToolsBuildOutputs.FromProject(projectNode.ProjectInstance, logger) is not { } browserToolsOutputs)
        {
            // The application has no pinned key, so nothing in the browser would ever connect to a
            // provider. This is not an error: the project simply does not opt into browser tools.
            return null;
        }

        return new BrowserRefreshServer(
            logger,
            connectionServerLoggerFactory: connectionId => context.LoggerFactory.CreateLogger(ConnectionServerLogComponentName, GetBrowserLoggerName(connectionId)),
            connectionAgentLoggerFactory: connectionId => context.LoggerFactory.CreateLogger(ConnectionAgentLogComponentName, GetBrowserLoggerName(connectionId)),
            middlewareAssemblyPath: GetMiddlewareAssemblyPath(),
            dotnetPath: context.EnvironmentOptions.GetMuxerPath(),
            sessionKeyFactory: browserToolsOutputs.CreateSessionKey,
            webSocketConfig: context.EnvironmentOptions.BrowserWebSocketConfig,
            useGatewayProxy: HasGatewayProxy,
            suppressTimeouts: context.EnvironmentOptions.TestFlags != TestFlags.None);
    }

    private static string GetBrowserLoggerName(int connectionId)
        => $"Browser #{connectionId}";

    public bool IsServerSupported(ProjectGraphNode projectNode, ILogger logger)
    {
        if (context.EnvironmentOptions.SuppressBrowserRefresh)
        {
            if (ManagedHotReloadRequiresBrowserRefresh)
            {
                logger.Log(MessageDescriptor.BrowserRefreshSuppressedViaEnvironmentVariable_ApplicationWillBeRestarted, EnvironmentVariables.Names.SuppressBrowserRefresh);
            }
            else
            {
                logger.Log(MessageDescriptor.BrowserRefreshSuppressedViaEnvironmentVariable_ManualRefreshRequired, EnvironmentVariables.Names.SuppressBrowserRefresh);
            }

            return false;
        }

        if (!projectNode.IsNetCoreApp(minVersion: s_minimumSupportedVersion))
        {
            if (ManagedHotReloadRequiresBrowserRefresh)
            {
                logger.Log(MessageDescriptor.BrowserRefreshNotSupportedByProjectTargetFramework_ApplicationWillBeRestarted);
            }
            else
            {
                logger.Log(MessageDescriptor.BrowserRefreshNotSupportedByProjectTargetFramework_ManualRefreshRequired);
            }

            return false;
        }

        logger.Log(MessageDescriptor.UsingBrowserRefreshMiddleware);
        return true;
    }
}

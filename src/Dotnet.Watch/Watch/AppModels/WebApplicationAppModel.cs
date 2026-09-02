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

    // This needs to be in sync with the version BrowserRefreshMiddleware is compiled against.
    private static readonly Version s_minimumSupportedVersion = Versions.Version6_0;
    private const string MiddlewareTargetFramework = "net6.0";

    public DotNetWatchContext Context => context;

    public abstract bool ManagedHotReloadRequiresBrowserRefresh { get; }

    /// <summary>
    /// Project that's used for launching the application.
    /// </summary>
    public abstract ProjectGraphNode LaunchingProject { get; }

    internal virtual bool UsesBrowserToolsProvider
        => LaunchingProject.IsNetCoreApp(Versions.Version11_0);

    protected abstract ImmutableArray<HotReloadClient> CreateManagedClients(ILogger clientLogger, ILogger agentLogger, BrowserRefreshServer? browserRefreshServer);

    public async sealed override ValueTask<HotReloadClients> CreateClientsAsync(ILogger clientLogger, ILogger agentLogger, CancellationToken cancellationToken)
    {
        var browserRefreshServer = await context.BrowserRefreshServerFactory.GetOrCreateBrowserRefreshServerAsync(LaunchingProject, this, cancellationToken);

        var managedClients = (!ManagedHotReloadRequiresBrowserRefresh || browserRefreshServer != null) && IsManagedAgentSupported(LaunchingProject, clientLogger)
            ? CreateManagedClients(clientLogger, agentLogger, browserRefreshServer)
            : [];

        var launchConfigurator = browserRefreshServer != null
            ? CreateBrowserToolsLaunchConfigurator(
                browserRefreshServer,
                enableManagedHotReload: !managedClients.IsEmpty)
            : null;

        return new HotReloadClients(
            managedClients,
            browserRefreshServer,
            useRefreshServerToApplyStaticAssets: true,
            launchConfigurator);
    }

    protected WebAssemblyHotReloadClient CreateWebAssemblyClient(
        ILogger clientLogger,
        ILogger agentLogger,
        BrowserRefreshServer browserRefreshServer,
        ProjectGraphNode clientProject,
        bool enableBrowserToolsReplay = false)
    {
        var capabilities = clientProject.GetWebAssemblyCapabilities().ToImmutableArray();
        var targetFramework = clientProject.GetTargetFrameworkVersion() ?? throw new InvalidOperationException($"Project doesn't define {PropertyNames.TargetFrameworkMoniker}");
        var generationId = browserRefreshServer.ResetBrowserToolsGeneration();

        return new WebAssemblyHotReloadClient(
            clientLogger,
            agentLogger,
            browserRefreshServer,
            generationId,
            enableBrowserToolsReplay,
            capabilities,
            targetFramework,
            context.EnvironmentOptions.TestFlags.HasFlag(TestFlags.MockBrowser));
    }

    private static string GetMiddlewareAssemblyPath()
        => GetInjectedAssemblyPath(MiddlewareTargetFramework, "Microsoft.AspNetCore.Watch.BrowserRefresh");

    internal virtual IBrowserToolsLaunchConfigurator CreateBrowserToolsLaunchConfigurator(
        AbstractBrowserRefreshServer browserRefreshServer,
        bool enableManagedHotReload)
        => UsesBrowserToolsProvider
            ? new ForwardingBrowserToolsLaunchConfigurator(GetMiddlewareAssemblyPath(), browserRefreshServer)
            : CreateLegacyBrowserToolsLaunchConfigurator(browserRefreshServer, enableManagedHotReload);

    protected static IBrowserToolsLaunchConfigurator CreateLegacyBrowserToolsLaunchConfigurator(
        AbstractBrowserRefreshServer browserRefreshServer,
        bool enableManagedHotReload)
        => new HostingStartupBrowserToolsLaunchConfigurator(GetMiddlewareAssemblyPath(), browserRefreshServer, enableManagedHotReload);

    public BrowserRefreshServer? TryCreateRefreshServer(ProjectGraphNode projectNode)
    {
        var logger = context.LoggerFactory.CreateLogger(ServerLogComponentName, projectNode.GetDisplayName());

        if (IsServerSupported(projectNode, logger))
        {
            var webSocketConfig = UsesBrowserToolsProvider
                ? context.EnvironmentOptions.BrowserWebSocketConfig.WithHostName(value: null)
                : context.EnvironmentOptions.BrowserWebSocketConfig;

            return new BrowserRefreshServer(
                logger,
                connectionServerLoggerFactory: connectionId => context.LoggerFactory.CreateLogger(ConnectionServerLogComponentName, GetBrowserLoggerName(connectionId)),
                connectionAgentLoggerFactory: connectionId => context.LoggerFactory.CreateLogger(ConnectionAgentLogComponentName, GetBrowserLoggerName(connectionId)),
                middlewareAssemblyPath: GetMiddlewareAssemblyPath(),
                dotnetPath: context.EnvironmentOptions.GetMuxerPath(),
                webSocketConfig,
                suppressTimeouts: context.EnvironmentOptions.TestFlags != TestFlags.None);
        }

        return null;
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

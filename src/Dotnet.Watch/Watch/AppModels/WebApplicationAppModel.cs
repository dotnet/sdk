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
    /// Configures the application process to expose the browser tools provider on its own origin.
    /// The default forwards the provider routes from a hosting startup injected into the app.
    /// </summary>
    internal virtual void ConfigureBrowserToolsLaunchEnvironment(IDictionary<string, string> environment, AbstractBrowserRefreshServer browserRefreshServer)
        => AddHostingStartupEnvironment(environment, browserRefreshServer, GetMiddlewareAssemblyPath());

    internal static void AddHostingStartupEnvironment(IDictionary<string, string> environment, AbstractBrowserRefreshServer browserRefreshServer, string middlewareAssemblyPath)
    {
        environment[MiddlewareEnvironmentVariables.AspNetCoreAutoReloadProviderAddress] = browserRefreshServer.ProviderAddress.AbsoluteUri;

        // Loading the assembly as a startup hook makes the out-of-application BrowserRefresh
        // assembly resolvable when ASP.NET Core activates its hosting startup by simple name.
        environment.InsertListItem(MiddlewareEnvironmentVariables.DotNetStartupHooks, middlewareAssemblyPath, Path.PathSeparator);
        environment.InsertListItem(MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssemblies, Path.GetFileNameWithoutExtension(middlewareAssemblyPath), MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssembliesSeparator);

        if (browserRefreshServer.Logger.IsEnabled(LogLevel.Trace))
        {
            // enable debug logging from the hosting startup:
            environment[MiddlewareEnvironmentVariables.LoggingLevel] = "Debug";
        }
    }

    /// <summary>
    /// Creates the browser tools provider for the project. The application host is configured by
    /// <see cref="ConfigureBrowserToolsLaunchEnvironment"/> to expose it on the app's own origin.
    ///
    /// The provider is keyed with the private half of the key pair the project's build produced, so
    /// that it can authenticate against the public half the application pinned into its build
    /// output. Consequently the provider can only be created once the project has been built.
    /// </summary>
    /// <exception cref="BrowserToolsBuildOutputsException">
    /// The project produces browser tools assets but the key pair the build wrote is missing,
    /// malformed or mismatched.
    /// </exception>
    public BrowserRefreshServer? TryCreateRefreshServer(ProjectGraphNode projectNode)
    {
        var logger = context.LoggerFactory.CreateLogger(ServerLogComponentName, projectNode.GetDisplayName());

        if (!IsServerSupported(projectNode, logger))
        {
            return null;
        }

        if (BrowserToolsBuildOutputs.TryGetFor(projectNode, logger) is not { } browserToolsOutputs)
        {
            // The application has no pinned key, so nothing in the browser would ever connect to a
            // provider. This is not an error: the project simply does not opt into browser tools.
            return null;
        }

        var sessionKey = browserToolsOutputs.CreateSessionKey();

        try
        {
            return new BrowserRefreshServer(
                logger,
                connectionServerLoggerFactory: connectionId => context.LoggerFactory.CreateLogger(ConnectionServerLogComponentName, GetBrowserLoggerName(connectionId)),
                connectionAgentLoggerFactory: connectionId => context.LoggerFactory.CreateLogger(ConnectionAgentLogComponentName, GetBrowserLoggerName(connectionId)),
                configureLaunchEnvironment: ConfigureBrowserToolsLaunchEnvironment,
                dotnetPath: context.EnvironmentOptions.GetMuxerPath(),
                sessionKey: sessionKey,
                webSocketConfig: context.EnvironmentOptions.BrowserWebSocketConfig,
                suppressTimeouts: context.EnvironmentOptions.TestFlags != TestFlags.None);
        }
        catch
        {
            sessionKey.Dispose();
            throw;
        }
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

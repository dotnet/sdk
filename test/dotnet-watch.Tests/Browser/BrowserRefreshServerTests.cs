// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class BrowserRefreshServerTests
{
    class TestListener : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static async ValueTask<TestBrowserRefreshServer> CreateStartedServerAsync(
        Action<IDictionary<string, string>, AbstractBrowserRefreshServer> configureLaunchEnvironment,
        LogLevel enabledLogLevel = LogLevel.Information)
    {
        var server = new TestBrowserRefreshServer(configureLaunchEnvironment)
        {
            CreateAndStartHostImpl = () => new WebServerHost(new TestListener(), ["ws://test.endpoint"], ["http://test.endpoint"])
        };

        ((TestLogger)server.Logger).IsEnabledImpl = level => level == enabledLogLevel;

        await server.StartAsync(CancellationToken.None);
        return server;
    }

    /// <summary>
    /// The server owns no knowledge of how the application is made to expose the provider routes:
    /// it delegates to the app model supplied callback.
    /// </summary>
    [TestMethod]
    public async Task ConfigureLaunchEnvironment_DelegatesToAppModel()
    {
        AbstractBrowserRefreshServer? observedServer = null;

        var server = await CreateStartedServerAsync((environment, s) =>
        {
            observedServer = s;
            environment["CUSTOM"] = s.ProviderAddress.AbsoluteUri;
        });

        var envBuilder = new Dictionary<string, string>();
        server.ConfigureLaunchEnvironment(envBuilder);

        Assert.AreSame(server, observedServer);
        AssertEx.SequenceEqual(["CUSTOM=http://test.endpoint/"], envBuilder.Select(e => $"{e.Key}={e.Value}"));
    }

    [TestMethod]
    [CombinatorialData]
    public async Task HostingStartupEnvironment(LogLevel logLevel)
    {
        var middlewarePath = Path.GetTempPath();
        var middlewareFileName = Path.GetFileNameWithoutExtension(middlewarePath);

        var server = await CreateStartedServerAsync(
            (environment, s) => WebApplicationAppModel.AddHostingStartupEnvironment(environment, s, middlewarePath),
            enabledLogLevel: logLevel);

        var envBuilder = new Dictionary<string, string>();
        server.ConfigureLaunchEnvironment(envBuilder);

        var expected = new List<string>()
        {
            "ASPNETCORE_AUTO_RELOAD_PROVIDER_ADDRESS=http://test.endpoint/",
            "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=" + middlewareFileName,
            "DOTNET_STARTUP_HOOKS=" + middlewarePath,
        };

        if (logLevel == LogLevel.Trace)
        {
            expected.Add("Logging__LogLevel__Microsoft.AspNetCore.Watch=Debug");
        }

        AssertEx.SequenceEqual(expected.Order(), envBuilder.OrderBy(e => e.Key).Select(e => $"{e.Key}={e.Value}"));
    }

    /// <summary>
    /// A standalone WebAssembly app is served by blazor-gateway, which does not activate hosting
    /// startups. Without the proxy route the provider routes fall through to the SPA fallback.
    /// </summary>
    [TestMethod]
    public async Task GatewayProxyEnvironment()
    {
        var server = await CreateStartedServerAsync(BlazorWebAssemblyAppModel.AddGatewayProxyEnvironment);

        var envBuilder = new Dictionary<string, string>();
        server.ConfigureLaunchEnvironment(envBuilder);

        AssertEx.SequenceEqual(
            [
                "ReverseProxy__Clusters__dotnet-browser-tools__Destinations__provider__Address=http://test.endpoint/",
                "ReverseProxy__Routes__dotnet-browser-tools__ClusterId=dotnet-browser-tools",
                "ReverseProxy__Routes__dotnet-browser-tools__Match__Path=/_framework/dotnet-browser-tools/{**catch-all}",
                "ReverseProxy__Routes__dotnet-browser-tools__Order=-1000",
            ],
            envBuilder.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}={e.Value}"));
    }
}

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
        string middlewareAssemblyPath,
        bool useGatewayProxy,
        LogLevel enabledLogLevel = LogLevel.Information)
    {
        var server = new TestBrowserRefreshServer(middlewareAssemblyPath, useGatewayProxy)
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
    /// <summary>
    /// dotnet-watch reports an update as applied when a browser acknowledges it. A browser that could
    /// not apply the update must therefore fail the acknowledgement instead of answering with an empty
    /// log, which would be indistinguishable from a successful apply.
    /// </summary>
    [TestMethod]
    public void ReceiveUpdateApplyResponse_BrowserReportsFailure()
    {
        var logger = new TestLogger();
        var response = """
            {"success":false,"log":[{"message":"Unable to apply managed code updates because this build of the app does not support runtime metadata updates.","severity":2}]}
            """u8;

        var success = AbstractBrowserRefreshServer.ReceiveUpdateApplyResponse(response, logger);

        Assert.IsFalse(success);
        Assert.IsTrue(logger.HasError);
        Assert.IsTrue(logger.GetAndClearMessages().Any(m => m.Contains("does not support runtime metadata updates")));
    }

    [TestMethod]
    public void ReceiveUpdateApplyResponse_BrowserReportsSuccess()
    {
        var logger = new TestLogger();
        var response = """
            {"success":true,"log":[]}
            """u8;

        var success = AbstractBrowserRefreshServer.ReceiveUpdateApplyResponse(response, logger);

        Assert.IsTrue(success);
        Assert.IsFalse(logger.HasError);
    }

    [TestMethod]
    [CombinatorialData]
    public async Task HostingStartupEnvironment(LogLevel logLevel, bool useGatewayProxy)
    {
        var middlewarePath = Path.GetTempPath();
        var middlewareFileName = Path.GetFileNameWithoutExtension(middlewarePath);

        var server = await CreateStartedServerAsync(
            middlewarePath,
            useGatewayProxy,
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

        if (useGatewayProxy)
        {
            AssertEx.SequenceEqual(
            [
                "ReverseProxy__Clusters__dotnet-browser-tools__Destinations__provider__Address=http://test.endpoint/",
                "ReverseProxy__Routes__dotnet-browser-tools__ClusterId=dotnet-browser-tools",
                "ReverseProxy__Routes__dotnet-browser-tools__Match__Path=/_framework/dotnet-browser-tools/{**catch-all}",
                "ReverseProxy__Routes__dotnet-browser-tools__Order=-1000",
            ],
            envBuilder.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key}={e.Value}"));
        }

        AssertEx.SequenceEqual(expected.Order(), envBuilder.OrderBy(e => e.Key).Select(e => $"{e.Key}={e.Value}"));
    }
}

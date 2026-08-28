// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
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

    [TestMethod]
    [CombinatorialData]
    public async Task ConfigureLaunchEnvironmentAsync(LogLevel logLevel, bool enableHotReload) 
    {
        var middlewarePath = Path.GetTempPath();
        var middlewareFileName = Path.GetFileNameWithoutExtension(middlewarePath);

        var server = new TestBrowserRefreshServer(middlewarePath)
        {
            CreateAndStartHostImpl = () => new WebServerHost(new TestListener(), ["http://test.endpoint"], virtualDirectory: "/test/virt/dir")
        };

        ((TestLogger)server.Logger).IsEnabledImpl = level => level == logLevel;

        await server.StartAsync(CancellationToken.None);

        var envBuilder = new Dictionary<string, string>();
        server.ConfigureLaunchEnvironment(envBuilder, enableHotReload);

        Assert.IsTrue(envBuilder.Remove("ASPNETCORE_AUTO_RELOAD_WS_KEY"));

        var expected = new List<string>()
        {
            "ASPNETCORE_AUTO_RELOAD_VDIR=/test/virt/dir",
            "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT=http://test.endpoint",
            "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=" + middlewareFileName,
            "DOTNET_STARTUP_HOOKS=" + middlewarePath,
        };

        if (enableHotReload)
        {
            expected.Add("DOTNET_MODIFIABLE_ASSEMBLIES=debug");
        }

        if (logLevel == LogLevel.Trace)
        {
            expected.Add("Logging__LogLevel__Microsoft.AspNetCore.Watch=Debug");
        }

        AssertEx.SequenceEqual(expected.Order(), envBuilder.OrderBy(e => e.Key).Select(e => $"{e.Key}={e.Value}"));
    }

    [TestMethod]
    public async Task LaunchUrlBootstrapDoesNotConfigureMiddleware()
    {
        var middlewarePath = Path.Combine(Path.GetTempPath(), "Microsoft.AspNetCore.Watch.BrowserRefresh.dll");
        var server = new TestBrowserRefreshServer(middlewarePath, useLaunchUrlBootstrap: true)
        {
            CreateAndStartHostImpl = () => new WebServerHost(new TestListener(), ["ws://localhost:1234", "wss://localhost:5678"], virtualDirectory: "/")
        };

        await server.StartAsync(CancellationToken.None);

        var environment = new Dictionary<string, string>
        {
            [MiddlewareEnvironmentVariables.AspNetCoreAutoReloadWSEndPoint] = "stale",
            [MiddlewareEnvironmentVariables.AspNetCoreAutoReloadWSKey] = "stale",
            [MiddlewareEnvironmentVariables.AspNetCoreAutoReloadVirtualDirectory] = "stale",
            [MiddlewareEnvironmentVariables.LoggingLevel] = "stale",
            [MiddlewareEnvironmentVariables.DotNetStartupHooks] = middlewarePath + Path.PathSeparator + "Other.StartupHook.dll",
            [MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssemblies] = "Microsoft.AspNetCore.Watch.BrowserRefresh;Other.HostingStartup",
        };

        server.ConfigureLaunchEnvironment(environment, enableHotReload: true);

        AssertEx.SequenceEqual(
            [
                new KeyValuePair<string, string>(MiddlewareEnvironmentVariables.DotNetStartupHooks, "Other.StartupHook.dll"),
                new KeyValuePair<string, string>(MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssemblies, "Other.HostingStartup"),
            ],
            environment);

        var launchUrl = server.AddLaunchUrlBootstrap("https://localhost/app#route");
        var fragmentParts = new Uri(launchUrl).Fragment.TrimStart('#').Split('&');
        Assert.AreEqual("route", fragmentParts[0]);

        const string parameter = "__dotnet_watch=";
        Assert.IsTrue(fragmentParts[1].StartsWith(parameter, StringComparison.Ordinal));

        using var config = JsonDocument.Parse(Uri.UnescapeDataString(fragmentParts[1][parameter.Length..]));
        Assert.AreEqual("ws://localhost:1234,wss://localhost:5678", config.RootElement.GetProperty("webSocketUrls").GetString());
        Assert.IsFalse(string.IsNullOrEmpty(config.RootElement.GetProperty("serverKey").GetString()));

        server.ConfigureLaunchUrlBootstrap(browserWillBeLaunched: false);
        environment.Clear();
        server.ConfigureLaunchEnvironment(environment, enableHotReload: true);

        Assert.IsTrue(environment.ContainsKey(MiddlewareEnvironmentVariables.DotNetStartupHooks));
        Assert.IsTrue(environment.ContainsKey(MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssemblies));
        Assert.AreEqual("https://localhost/app", server.AddLaunchUrlBootstrap("https://localhost/app"));
    }
}

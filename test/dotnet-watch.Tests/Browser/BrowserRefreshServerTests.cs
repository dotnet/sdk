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
            "ASPNETCORE_AUTO_RELOAD_USE_LEGACY_HTML_INJECTION=True",
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
    public async Task ConfigureLaunchEnvironmentAsync_InsertsIntoExistingEnvironment()
    {
        var middlewarePath = Path.Combine(Path.GetTempPath(), "Microsoft.AspNetCore.Watch.BrowserRefresh.dll");
        var middlewareAssemblyName = Path.GetFileNameWithoutExtension(middlewarePath);

        using var server = new TestBrowserRefreshServer(middlewarePath)
        {
            CreateAndStartHostImpl = () => new WebServerHost(new TestListener(), ["http://test.endpoint"], virtualDirectory: "/test/virt/dir")
        };
        ((TestLogger)server.Logger).IsEnabledImpl = _ => false;
        await server.StartAsync(CancellationToken.None);

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_STARTUP_HOOKS"] = "existing-hook",
            ["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "Existing.HostingStartup",
            ["UNRELATED_SETTING"] = "preserved",
        };

        server.ConfigureLaunchEnvironment(environment, enableHotReload: false);

        Assert.IsTrue(environment.Remove("ASPNETCORE_AUTO_RELOAD_WS_KEY"));
        AssertEx.SequenceEqual(
        [
            "ASPNETCORE_AUTO_RELOAD_USE_LEGACY_HTML_INJECTION=True",
            "ASPNETCORE_AUTO_RELOAD_VDIR=/test/virt/dir",
            "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT=http://test.endpoint",
            $"ASPNETCORE_HOSTINGSTARTUPASSEMBLIES={middlewareAssemblyName};Existing.HostingStartup",
            $"DOTNET_STARTUP_HOOKS={middlewarePath}{Path.PathSeparator}existing-hook",
            "UNRELATED_SETTING=preserved",
        ], environment.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"));
    }
}

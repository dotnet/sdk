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
    [DataRow(LogLevel.Trace, true)]
    [DataRow(LogLevel.Trace, false)]
    [DataRow(LogLevel.Debug, true)]
    [DataRow(LogLevel.Debug, false)]
    [DataRow(LogLevel.Information, true)]
    [DataRow(LogLevel.Information, false)]
    [DataRow(LogLevel.Warning, true)]
    [DataRow(LogLevel.Warning, false)]
    [DataRow(LogLevel.Error, true)]
    [DataRow(LogLevel.Error, false)]
    [DataRow(LogLevel.Critical, true)]
    [DataRow(LogLevel.Critical, false)]
    [DataRow(LogLevel.None, true)]
    [DataRow(LogLevel.None, false)]
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
}

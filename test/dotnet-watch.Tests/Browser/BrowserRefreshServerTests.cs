// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;
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
    public async Task ForwardingConfigurator_UsesOnlyProviderAddressAndAssemblyActivation()
    {
        var middlewarePath = Path.Combine(Path.GetTempPath(), "Microsoft.AspNetCore.Watch.BrowserRefresh.dll");
        using var server = new TestBrowserRefreshServer()
        {
            CreateAndStartHostImpl = () => new WebServerHost(
                new TestListener(),
                webSocketEndpoints: ["ws://127.0.0.1:1234"],
                httpEndpoints: ["http://127.0.0.1:1234"])
        };
        ((TestLogger)server.Logger).IsEnabledImpl = _ => false;
        await server.StartAsync(CancellationToken.None);
        var configurator = new ForwardingBrowserToolsLaunchConfigurator(middlewarePath, server);
        var environment = new Dictionary<string, string>();

        configurator.ConfigureLaunchEnvironment(environment);

        AssertEx.SequenceEqual(
        [
            "ASPNETCORE_AUTO_RELOAD_PROVIDER_ADDRESS=http://127.0.0.1:1234/",
            "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES=Microsoft.AspNetCore.Watch.BrowserRefresh",
            $"DOTNET_STARTUP_HOOKS={middlewarePath}",
        ], environment.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"));
    }
}

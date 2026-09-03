// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public sealed class BlazorWebAssemblyAppModelTests : DotNetWatchTestBase
{
    private sealed class TestListener : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static async Task<TestBrowserRefreshServer> StartServerAsync(CancellationToken cancellationToken)
    {
        var server = new TestBrowserRefreshServer()
        {
            CreateAndStartHostImpl = () => new WebServerHost(new TestListener(), ["ws://127.0.0.1:1234"], ["http://127.0.0.1:1234"])
        };

        ((TestLogger)server.Logger).IsEnabledImpl = _ => false;
        await server.StartAsync(cancellationToken);
        return server;
    }

    [TestMethod]
    public async Task StandaloneWasm_ForwardsFromHostingStartup()
    {
        var appModel = CreateAppModel("net10.0");
        using var server = await StartServerAsync(TestContext.CancellationToken);

        var environment = new Dictionary<string, string>();
        appModel.ConfigureBrowserToolsLaunchEnvironment(environment, server);

        AssertForwardingEnvironment(environment);
    }

    /// <summary>
    /// Server-hosted apps forward the provider routes from a hosting startup injected into the app process.
    /// </summary>
    [TestMethod]
    public async Task ServerHosted_ForwardsFromHostingStartup()
    {
        var wasmAppModel = CreateAppModel("net10.0");
        var appModel = new WebServerAppModel(wasmAppModel.Context, wasmAppModel.LaunchingProject);
        using var server = await StartServerAsync(TestContext.CancellationToken);

        var environment = new Dictionary<string, string>();
        appModel.ConfigureBrowserToolsLaunchEnvironment(environment, server);

        AssertForwardingEnvironment(environment);
    }

    private static void AssertForwardingEnvironment(Dictionary<string, string> environment)
    {
        Assert.AreEqual("http://127.0.0.1:1234/", environment["ASPNETCORE_AUTO_RELOAD_PROVIDER_ADDRESS"]);
        Assert.AreEqual("Microsoft.AspNetCore.Watch.BrowserRefresh", environment["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"]);
        Assert.EndsWith("Microsoft.AspNetCore.Watch.BrowserRefresh.dll", environment["DOTNET_STARTUP_HOOKS"]);
        Assert.DoesNotContain(key => key.StartsWith("ReverseProxy__", StringComparison.Ordinal), environment.Keys);
    }

    private BlazorWebAssemblyAppModel CreateAppModel(string targetFramework)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm", targetFramework)
            .WithSource(targetFramework: targetFramework);
        var projectPath = Path.Combine(testAsset.Path, "blazorwasm.csproj");
        var projectRepresentation = new ProjectRepresentation(projectPath, entryPointFilePath: null);
        var factory = new ProjectGraphFactory(
            [projectRepresentation],
            buildProperties: [],
            new TestLogger(),
            TestOptions.GlobalOptions,
            TestOptions.GetEnvironmentOptions(asset: testAsset));

        var graph = factory.TryLoadProjectGraph(
            projectGraphRequired: true,
            virtualProjectTargetFramework: null,
            TestContext.CancellationToken);
        Assert.IsNotNull(graph);

        return new BlazorWebAssemblyAppModel(context: null!, graph.Graph.GraphRoots.Single());
    }
}

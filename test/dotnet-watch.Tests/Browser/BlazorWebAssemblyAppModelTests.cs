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

    [TestMethod]
    public async Task CreateBrowserToolsLaunchConfigurator_StandaloneWasm_UsesOnlyGatewayEnvironment()
    {
        var appModel = CreateAppModel("net11.0");
        using var server = new TestBrowserRefreshServer()
        {
            CreateAndStartHostImpl = () => new WebServerHost(
                new TestListener(),
                webSocketEndpoints: ["ws://127.0.0.1:1234"],
                httpEndpoints: ["http://127.0.0.1:1234"])
        };
        await server.StartAsync(TestContext.CancellationToken);

        var configurator = appModel.CreateBrowserToolsLaunchConfigurator(server);

        Assert.IsInstanceOfType<GatewayProxyBrowserToolsLaunchConfigurator>(configurator);

        var environment = new Dictionary<string, string>();
        configurator.ConfigureLaunchEnvironment(environment);

        Assert.HasCount(4, environment);
        AssertEx.SequenceEqual(
        [
            "ReverseProxy__Clusters__dotnet-browser-tools__Destinations__provider__Address=http://127.0.0.1:1234/",
            "ReverseProxy__Routes__dotnet-browser-tools__ClusterId=dotnet-browser-tools",
            "ReverseProxy__Routes__dotnet-browser-tools__Match__Path=/_framework/dotnet-browser-tools/{**catch-all}",
            "ReverseProxy__Routes__dotnet-browser-tools__Order=-1000",
        ], environment.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}={entry.Value}"));

        Assert.IsFalse(environment.ContainsKey(MiddlewareEnvironmentVariables.DotNetStartupHooks));
        Assert.IsFalse(environment.ContainsKey(MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssemblies));
        Assert.IsFalse(environment.ContainsKey(MiddlewareEnvironmentVariables.LoggingLevel));
        Assert.DoesNotContain(
            key => key.StartsWith("ASPNETCORE_AUTO_RELOAD_", StringComparison.Ordinal),
            environment.Keys);
    }

    [TestMethod]
    public void CreateBrowserToolsLaunchConfigurator_PreNet11StandaloneWasm_UsesGateway()
    {
        var appModel = CreateAppModel("net10.0");
        using var server = new TestBrowserRefreshServer();

        var configurator = appModel.CreateBrowserToolsLaunchConfigurator(server);

        Assert.IsInstanceOfType<GatewayProxyBrowserToolsLaunchConfigurator>(configurator);
    }

    [TestMethod]
    [DataRow("net8.0")]
    [DataRow("net9.0")]
    [DataRow("net10.0")]
    [DataRow("net11.0")]
    public void WebServer_UsesForwardingBrowserToolsLaunchStrategy(string targetFramework)
    {
        var wasmAppModel = CreateAppModel(targetFramework);
        var appModel = new WebServerAppModel(wasmAppModel.Context, wasmAppModel.LaunchingProject);
        using var browserRefreshServer = new TestBrowserRefreshServer();

        var configurator = appModel.CreateBrowserToolsLaunchConfigurator(browserRefreshServer);

        Assert.IsInstanceOfType<ForwardingBrowserToolsLaunchConfigurator>(configurator);
    }

    [TestMethod]
    [DataRow("net8.0", "net8.0")]
    [DataRow("net9.0", "net9.0")]
    [DataRow("net10.0", "net10.0")]
    [DataRow("net11.0", "net10.0")]
    [DataRow("net10.0", "net11.0")]
    [DataRow("net11.0", "net11.0")]
    public void HostedWasm_UsesForwardingBrowserToolsLaunchStrategy(
        string clientTargetFramework,
        string serverTargetFramework)
    {
        var clientAppModel = CreateAppModel(clientTargetFramework, identifierSuffix: "client");
        var serverAppModel = CreateAppModel(serverTargetFramework, identifierSuffix: "server");
        var appModel = new BlazorWebAssemblyHostedAppModel(
            clientAppModel.Context,
            clientAppModel.LaunchingProject,
            serverAppModel.LaunchingProject);
        using var browserRefreshServer = new TestBrowserRefreshServer();

        var configurator = appModel.CreateBrowserToolsLaunchConfigurator(browserRefreshServer);

        Assert.IsInstanceOfType<ForwardingBrowserToolsLaunchConfigurator>(configurator);
    }

    private BlazorWebAssemblyAppModel CreateAppModel(string targetFramework, string? identifierSuffix = null)
    {
        var identifier = identifierSuffix is null ? targetFramework : $"{targetFramework}-{identifierSuffix}";
        var testAsset = TestAssets.CopyTestAsset("WatchBlazorWasm", identifier)
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

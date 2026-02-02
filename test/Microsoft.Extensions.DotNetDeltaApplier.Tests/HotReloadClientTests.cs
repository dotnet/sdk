// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch.UnitTests;

public class HotReloadClientTests(ITestOutputHelper output)
{
    private sealed class Test : IAsyncDisposable
    {
        public readonly TestLogger Logger;
        public readonly TestLogger AgentLogger;
        public readonly DefaultHotReloadClient Client;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Task<Task> _listenerTaskFactory;

        public Test(ITestOutputHelper output, TestHotReloadAgent agent)
        {
            Logger = new TestLogger(output);
            AgentLogger = new TestLogger(output);
            Client = new DefaultHotReloadClient(Logger, AgentLogger, startupHookPath: "", enableStaticAssetUpdates: true);

            _cancellationSource = new CancellationTokenSource();

            Client.InitiateConnection(CancellationToken.None);
            var listener = new PipeListener(Client.NamedPipeName, agent, log: _ => { }, connectionTimeoutMS: Timeout.Infinite);
            _listenerTaskFactory = Task.Run<Task>(() => listener.Listen(_cancellationSource.Token));
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationSource.Cancel();
            await await _listenerTaskFactory;

            Client.Dispose();
        }
    }

    [Fact]
    public async Task ApplyManagedCodeUpdates()
    {
        var moduleId = Guid.NewGuid();

        var agent = new TestHotReloadAgent()
        {
            Capabilities = "Baseline AddMethodToExistingType AddStaticFieldToExistingType",
        };

        await using var test = new Test(output, agent);

        var actualCapabilities = await test.Client.GetUpdateCapabilitiesAsync(CancellationToken.None);
        AssertEx.SequenceEqual(["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType", "AddExplicitInterfaceImplementation"], actualCapabilities);

        var update = new HotReloadManagedCodeUpdate(
            moduleId: moduleId,
            metadataDelta: [1, 2, 3],
            ilDelta: [],
            pdbDelta: [],
            updatedTypes: [],
            requiredCapabilities: ["Baseline"]);

        var agentMessage = "[Debug] Writing capabilities: Baseline AddMethodToExistingType AddStaticFieldToExistingType";

        await await test.Client.ApplyManagedCodeUpdatesAsync([update], CancellationToken.None, CancellationToken.None);

        var clientMessages = test.Logger.GetAndClearMessages();
        var agentMessages = test.AgentLogger.GetAndClearMessages();

        Assert.Contains("[Debug] " + string.Format(LogEvents.SendingUpdateBatch.Message, 0), clientMessages);
        Assert.Contains("[Debug] " + string.Format(LogEvents.UpdateBatchCompleted.Message, 0), clientMessages);
        Assert.Contains(agentMessage, agentMessages);
    }

    [Fact]
    public async Task ApplyManagedCodeUpdates_Failure()
    {
        var agent = new TestHotReloadAgent()
        {
            Capabilities = "Baseline AddMethodToExistingType AddStaticFieldToExistingType",
            ApplyManagedCodeUpdatesImpl = updates => throw new Exception("Bug!")
        };

        await using var test = new Test(output, agent);

        var actualCapabilities = await test.Client.GetUpdateCapabilitiesAsync(CancellationToken.None);
        AssertEx.SequenceEqual(["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType", "AddExplicitInterfaceImplementation"], actualCapabilities);

        var update = new HotReloadManagedCodeUpdate(
            moduleId: Guid.NewGuid(),
            metadataDelta: [],
            ilDelta: [],
            pdbDelta: [],
            updatedTypes: [],
            requiredCapabilities: ["Baseline"]);

        await await test.Client.ApplyManagedCodeUpdatesAsync([update], CancellationToken.None, CancellationToken.None);

        var agentMessages = test.AgentLogger.GetAndClearMessages();
        Assert.Contains("[Error] The runtime failed to applying the change: Bug!", agentMessages);
        Assert.Contains("[Warning] Further changes won't be applied to this process.", agentMessages);
    }
}

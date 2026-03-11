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
    public async Task ApplyManagedCodeUpdates_ProcessNotSuspended()
    {
        var moduleId = Guid.NewGuid();

        var agent = new TestHotReloadAgent()
        {
            Capabilities = "Baseline AddMethodToExistingType AddStaticFieldToExistingType",
            ApplyManagedCodeUpdatesImpl = updates =>
            {
                Assert.Single(updates);
                var update = updates.First();
                Assert.Equal(moduleId, update.ModuleId);
                AssertEx.SequenceEqual<byte>([1, 2, 3], update.MetadataDelta);
            }
        };

        await using var test = new Test(output, agent);

        var actualCapabilities = await test.Client.GetUpdateCapabilitiesAsync(CancellationToken.None);
        AssertEx.SequenceEqual(["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType"], actualCapabilities);

        var update = new HotReloadManagedCodeUpdate(
            moduleId: moduleId,
            metadataDelta: [1, 2, 3],
            ilDelta: [],
            pdbDelta: [],
            updatedTypes: [],
            requiredCapabilities: ["Baseline"]);

        Assert.Equal(ApplyStatus.AllChangesApplied, await test.Client.ApplyManagedCodeUpdatesAsync([update], isProcessSuspended: false, CancellationToken.None));

        Assert.Contains("[Debug] Writing capabilities: Baseline AddMethodToExistingType AddStaticFieldToExistingType", test.AgentLogger.GetAndClearMessages());
        Assert.Contains("[Debug] Updates applied: 1 out of 1.", test.Logger.GetAndClearMessages());
    }

    [Fact]
    public async Task ApplyManagedCodeUpdates_ProcessSuspended()
    {
        var moduleId = Guid.NewGuid();

        var agent = new TestHotReloadAgent()
        {
            Capabilities = "Baseline AddMethodToExistingType AddStaticFieldToExistingType",
        };

        await using var test = new Test(output, agent);

        var actualCapabilities = await test.Client.GetUpdateCapabilitiesAsync(CancellationToken.None);
        AssertEx.SequenceEqual(["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType"], actualCapabilities);

        var update = new HotReloadManagedCodeUpdate(
            moduleId: moduleId,
            metadataDelta: [1, 2, 3],
            ilDelta: [],
            pdbDelta: [],
            updatedTypes: [],
            requiredCapabilities: ["Baseline"]);

        var agentMessage = "[Debug] Writing capabilities: Baseline AddMethodToExistingType AddStaticFieldToExistingType";

        Assert.Equal(ApplyStatus.AllChangesApplied, await test.Client.ApplyManagedCodeUpdatesAsync([update], isProcessSuspended: true, CancellationToken.None));

        // emulate process being resumed:
        await test.Client.PendingUpdates;

        var clientMessages = test.Logger.GetAndClearMessages();
        var agentMessages = test.AgentLogger.GetAndClearMessages();

        // agent log messages not reported to the client logger while the process is suspended:
        Assert.Contains("[Debug] Sending update batch #0", clientMessages);
        Assert.Contains("[Debug] Updates applied: 1 out of 1.", clientMessages);
        Assert.Contains("[Debug] Update batch #0 completed.", clientMessages);
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
        AssertEx.SequenceEqual(["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType"], actualCapabilities);

        var update = new HotReloadManagedCodeUpdate(
            moduleId: Guid.NewGuid(),
            metadataDelta: [],
            ilDelta: [],
            pdbDelta: [],
            updatedTypes: [],
            requiredCapabilities: ["Baseline"]);

        Assert.Equal(ApplyStatus.Failed, await test.Client.ApplyManagedCodeUpdatesAsync([update], isProcessSuspended: false, CancellationToken.None));

        // agent log messages were reported to the agent logger:
        var agentMessages = test.AgentLogger.GetAndClearMessages();
        Assert.Contains("[Error] The runtime failed to applying the change: Bug!", agentMessages);
        Assert.Contains("[Warning] Further changes won't be applied to this process.", agentMessages);
    }
}

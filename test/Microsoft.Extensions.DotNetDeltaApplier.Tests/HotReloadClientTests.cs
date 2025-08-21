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
            var pipeName = Guid.NewGuid().ToString();
            Logger = new TestLogger(output);
            AgentLogger = new TestLogger(output);
            Client = new DefaultHotReloadClient(Logger, AgentLogger, enableStaticAssetUpdates: true);

            _cancellationSource = new CancellationTokenSource();

            Client.InitiateConnection(pipeName, CancellationToken.None);
            var listener = new PipeListener(pipeName, agent, log: _ => { }, connectionTimeoutMS: Timeout.Infinite);
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

        Assert.Contains("[Debug] Writing capabilities: Baseline AddMethodToExistingType AddStaticFieldToExistingType", test.AgentLogger.Messages);
        Assert.Contains("[Debug] Updates applied: 1 out of 1.", test.Logger.Messages);
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

        // agent log messages not reported to the client logger while the process is suspended:
        Assert.Contains("[Debug] Sending update batch #0", test.Logger.Messages);
        Assert.Contains("[Debug] Updates applied: 1 out of 1.", test.Logger.Messages);
        Assert.DoesNotContain(agentMessage, test.AgentLogger.Messages);
        test.AgentLogger.Messages.Clear();

        // emulate process being resumed:
        await test.Client.PendingUpdates;

        Assert.Contains(agentMessage, test.AgentLogger.Messages);
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
        Assert.Contains("[Error] The runtime failed to applying the change: Bug!", test.AgentLogger.Messages);
        Assert.Contains("[Warning] Further changes won't be applied to this process.", test.AgentLogger.Messages);
    }
}

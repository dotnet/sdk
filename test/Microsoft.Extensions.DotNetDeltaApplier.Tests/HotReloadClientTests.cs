// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Test.MSTest.Utilities;

namespace Microsoft.DotNet.HotReload.UnitTests;

[TestClass]
public class HotReloadClientTests
{
    public TestContext TestContext { get; set; } = null!;

    private sealed class Test : IAsyncDisposable
    {
        public const int ProcessId = 654321;
        public readonly TestLogger Logger;
        public readonly TestLogger AgentLogger;
        public readonly DefaultHotReloadClient Client;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Task<Task> _listenerTaskFactory;

        public Test(TestContext testContext, TestHotReloadAgent agent, bool hasRemoteAgent)
        {
            Logger = new TestLogger(testContext);
            AgentLogger = new TestLogger(testContext);
            var clientTransport = new NamedPipeClientTransport(Logger);
            Client = new DefaultHotReloadClient(Logger, AgentLogger, startupHookPath: "", transport: clientTransport, handlesStaticAssetUpdates: true, hasRemoteAgent);

            _cancellationSource = new CancellationTokenSource();

            Client.InitiateConnection(environmentVariables: [], CancellationToken.None);
            var agentTransport = new NamedPipeTransport(clientTransport.NamedPipeName, log: _ => { }, timeoutMS: Timeout.Infinite);
            var listener = new Listener(agentTransport, agent, ProcessId, log: _ => { });
            _listenerTaskFactory = Task.Run<Task>(() => listener.Listen(_cancellationSource.Token), testContext.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationSource.Cancel();
            try
            {
                await await _listenerTaskFactory;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested during disposal
            }

            Client.Dispose();
        }
    }

    [TestMethod]
    public async Task ApplyManagedCodeUpdates()
    {
        var moduleId = Guid.NewGuid();

        var agent = new TestHotReloadAgent()
        {
            Capabilities = "Baseline AddMethodToExistingType AddStaticFieldToExistingType",
        };

        await using var test = new Test(TestContext, agent, hasRemoteAgent: false);

        var agentInfo = await test.Client.GetConnectedAgentInfoAsync(CancellationToken.None);
        Assert.AreEqual(Test.ProcessId, agentInfo.LocalProcessId);
        Assert.AreSequenceEqual(["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType", "AddExplicitInterfaceImplementation"], agentInfo.ManagedCodeUpdateCapabilities);

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

    [TestMethod]
    public async Task ApplyManagedCodeUpdates_Failure()
    {
        var agent = new TestHotReloadAgent()
        {
            Capabilities = "Baseline AddMethodToExistingType AddStaticFieldToExistingType",
            ApplyManagedCodeUpdatesImpl = updates => throw new Exception("Bug!")
        };

        await using var test = new Test(TestContext, agent, hasRemoteAgent: true);

        var agentInfo = await test.Client.GetConnectedAgentInfoAsync(CancellationToken.None);
        Assert.IsNull(agentInfo.LocalProcessId);
        Assert.AreSequenceEqual(["Baseline", "AddMethodToExistingType", "AddStaticFieldToExistingType", "AddExplicitInterfaceImplementation"], agentInfo.ManagedCodeUpdateCapabilities);

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

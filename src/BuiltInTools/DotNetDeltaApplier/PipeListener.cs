// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;

namespace Microsoft.DotNet.HotReload;

internal sealed class PipeListener(string pipeName, IHotReloadAgent agent, Action<string> log, int connectionTimeoutMS = 5000)
{
    public Task Listen(CancellationToken cancellationToken)
    {
        // Connect to the pipe synchronously.
        //
        // If a debugger is attached and there is a breakpoint in the startup code connecting asynchronously would
        // set up a race between this code connecting to the server, and the breakpoint being hit. If the breakpoint
        // hits first, applying changes will throw an error that the client is not connected.
        //
        // Updates made before the process is launched need to be applied before loading the affected modules.

        log($"Connecting to hot-reload server via pipe {pipeName}");

        var pipeClient = new NamedPipeClientStream(serverName: ".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        try
        {
            pipeClient.Connect(connectionTimeoutMS);
            log("Connected.");
        }
        catch (TimeoutException)
        {
            log($"Failed to connect in {connectionTimeoutMS}ms.");
            pipeClient.Dispose();
            return Task.CompletedTask;
        }

        try
        {
            // block execution of the app until initial updates are applied:
            InitializeAsync(pipeClient, cancellationToken).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                log(e.Message);
            }

            pipeClient.Dispose();
            agent.Dispose();

            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            try
            {
                await ReceiveAndApplyUpdatesAsync(pipeClient, initialUpdates: false, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log(e.Message);
            }
            finally
            {
                pipeClient.Dispose();
                agent.Dispose();
            }
        }, cancellationToken);
    }

    private async Task InitializeAsync(NamedPipeClientStream pipeClient, CancellationToken cancellationToken)
    {
        agent.Reporter.Report("Writing capabilities: " + agent.Capabilities, AgentMessageSeverity.Verbose);

        var initPayload = new ClientInitializationResponse(agent.Capabilities);
        await initPayload.WriteAsync(pipeClient, cancellationToken);

        // Apply updates made before this process was launched to avoid executing unupdated versions of the affected modules.

        // We should only receive ManagedCodeUpdate when when the debugger isn't attached,
        // otherwise the initialization should send InitialUpdatesCompleted immediately.
        // The debugger itself applies these updates when launching process with the debugger attached.
        await ReceiveAndApplyUpdatesAsync(pipeClient, initialUpdates: true, cancellationToken);
    }

    private async Task ReceiveAndApplyUpdatesAsync(NamedPipeClientStream pipeClient, bool initialUpdates, CancellationToken cancellationToken)
    {
        while (pipeClient.IsConnected)
        {
            var payloadType = (RequestType)await pipeClient.ReadByteAsync(cancellationToken);
            switch (payloadType)
            {
                case RequestType.ManagedCodeUpdate:
                    await ReadAndApplyManagedCodeUpdateAsync(pipeClient, cancellationToken);
                    break;

                case RequestType.StaticAssetUpdate:
                    await ReadAndApplyStaticAssetUpdateAsync(pipeClient, cancellationToken);
                    break;

                case RequestType.InitialUpdatesCompleted when initialUpdates:
                    return;

                default:
                    // can't continue, the pipe content is in an unknown state
                    throw new InvalidOperationException($"Unexpected payload type: {payloadType}");
            }
        }
    }

    private async ValueTask ReadAndApplyManagedCodeUpdateAsync(
        NamedPipeClientStream pipeClient,
        CancellationToken cancellationToken)
    {
        var request = await ManagedCodeUpdateRequest.ReadAsync(pipeClient, cancellationToken);

        bool success;
        try
        {
            agent.ApplyManagedCodeUpdates(request.Updates);
            success = true;
        }
        catch (Exception e)
        {
            agent.Reporter.Report($"The runtime failed to applying the change: {e.Message}", AgentMessageSeverity.Error);
            agent.Reporter.Report("Further changes won't be applied to this process.", AgentMessageSeverity.Warning);
            success = false;
        }

        var logEntries = agent.Reporter.GetAndClearLogEntries(request.ResponseLoggingLevel);

        var response = new UpdateResponse(logEntries, success);
        await response.WriteAsync(pipeClient, cancellationToken);
    }

    private async ValueTask ReadAndApplyStaticAssetUpdateAsync(
        NamedPipeClientStream pipeClient,
        CancellationToken cancellationToken)
    {
        var request = await StaticAssetUpdateRequest.ReadAsync(pipeClient, cancellationToken);

        try
        {
            agent.ApplyStaticAssetUpdate(request.Update);
        }
        catch (Exception e)
        {
            agent.Reporter.Report($"Failed to apply static asset update: {e.Message}", AgentMessageSeverity.Error);
        }

        var logEntries = agent.Reporter.GetAndClearLogEntries(request.ResponseLoggingLevel);

        // Updating static asset only invokes ContentUpdate metadata update handlers.
        // Failures of these handlers are reported to the log and ignored.
        // Therefore, this request always succeeds.
        var response = new UpdateResponse(logEntries, success: true);

        await response.WriteAsync(pipeClient, cancellationToken);
    }
}

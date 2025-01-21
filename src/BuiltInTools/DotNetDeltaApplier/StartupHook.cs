// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using Microsoft.DotNet.HotReload;
using System.Diagnostics;

/// <summary>
/// The runtime startup hook looks for top-level type named "StartupHook".
/// </summary>
internal sealed class StartupHook
{
    private const int ConnectionTimeoutMS = 5000;

    private static readonly bool s_logToStandardOutput = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.HotReloadDeltaClientLogMessages) == "1";
    private static readonly string s_namedPipeName = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName);

    /// <summary>
    /// Invoked by the runtime when the containing assembly is listed in DOTNET_STARTUP_HOOKS.
    /// </summary>
    public static void Initialize()
    {
        var processPath = Environment.GetCommandLineArgs().FirstOrDefault();

        Log($"Loaded into process: {processPath}");

        HotReloadAgent.ClearHotReloadEnvironmentVariables(typeof(StartupHook));

        Log($"Connecting to hot-reload server");

        // Connect to the pipe synchronously.
        //
        // If a debugger is attached and there is a breakpoint in the startup code connecting asynchronously would
        // set up a race between this code connecting to the server, and the breakpoint being hit. If the breakpoint
        // hits first, applying changes will throw an error that the client is not connected.
        //
        // Updates made before the process is launched need to be applied before loading the affected modules. 

        var pipeClient = new NamedPipeClientStream(".", s_namedPipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
        try
        {
            pipeClient.Connect(ConnectionTimeoutMS);
            Log("Connected.");
        }
        catch (TimeoutException)
        {
            Log($"Failed to connect in {ConnectionTimeoutMS}ms.");
            return;
        }

        var agent = new HotReloadAgent();
        try
        {
            // block until initialization completes:
            InitializeAsync(pipeClient, agent, CancellationToken.None).GetAwaiter().GetResult();

            // fire and forget:
            _ = ReceiveAndApplyUpdatesAsync(pipeClient, agent, initialUpdates: false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log(ex.Message);
            pipeClient.Dispose();
        }
    }

    private static async ValueTask InitializeAsync(NamedPipeClientStream pipeClient, HotReloadAgent agent, CancellationToken cancellationToken)
    {
        agent.Reporter.Report("Writing capabilities: " + agent.Capabilities, AgentMessageSeverity.Verbose);

        var initPayload = new ClientInitializationResponse(agent.Capabilities);
        await initPayload.WriteAsync(pipeClient, cancellationToken);

        // Apply updates made before this process was launched to avoid executing unupdated versions of the affected modules.
        await ReceiveAndApplyUpdatesAsync(pipeClient, agent, initialUpdates: true, cancellationToken);
    }

    private static async Task ReceiveAndApplyUpdatesAsync(NamedPipeClientStream pipeClient, HotReloadAgent agent, bool initialUpdates, CancellationToken cancellationToken)
    {
        try
        {
            while (pipeClient.IsConnected)
            {
                var payloadType = (RequestType)await pipeClient.ReadByteAsync(cancellationToken);
                switch (payloadType)
                {
                    case RequestType.ManagedCodeUpdate:
                        // Shouldn't get initial managed code updates when the debugger is attached.
                        // The debugger itself applies these updates when launching process with the debugger attached.
                        Debug.Assert(!Debugger.IsAttached);
                        await ReadAndApplyManagedCodeUpdateAsync(pipeClient, agent, cancellationToken);
                        break;

                    case RequestType.StaticAssetUpdate:
                        await ReadAndApplyStaticAssetUpdateAsync(pipeClient, agent, cancellationToken);
                        break;

                    case RequestType.InitialUpdatesCompleted when initialUpdates:
                        return;

                    case RequestType.TerminateProcess:
                        Environment.Exit(exitCode: 0);
                        return;

                    default:
                        // can't continue, the pipe content is in an unknown state
                        Log($"Unexpected payload type: {payloadType}. Terminating agent.");
                        return;
                }
            }
        }
        catch (Exception ex)
        {
            Log(ex.Message);
        }
        finally
        {
            if (!pipeClient.IsConnected)
            {
                await pipeClient.DisposeAsync();
            }

            if (!initialUpdates)
            {
                agent.Dispose();
            }
        }
    }

    private static async ValueTask ReadAndApplyManagedCodeUpdateAsync(
        NamedPipeClientStream pipeClient,
        HotReloadAgent agent,
        CancellationToken cancellationToken)
    {
        var request = await ManagedCodeUpdateRequest.ReadAsync(pipeClient, cancellationToken);

        bool success;
        try
        {
            agent.ApplyDeltas(request.Deltas);
            success = true;
        }
        catch (Exception e)
        {
            agent.Reporter.Report($"The runtime failed to applying the change: {e.Message}", AgentMessageSeverity.Error);
            agent.Reporter.Report("Further changes won't be applied to this process.", AgentMessageSeverity.Warning);
            success = false;
        }

        var logEntries = agent.GetAndClearLogEntries(request.ResponseLoggingLevel);

        var response = new UpdateResponse(logEntries, success);
        await response.WriteAsync(pipeClient, cancellationToken);
    }

    private static async ValueTask ReadAndApplyStaticAssetUpdateAsync(
        NamedPipeClientStream pipeClient,
        HotReloadAgent agent,
        CancellationToken cancellationToken)
    {
        var request = await StaticAssetUpdateRequest.ReadAsync(pipeClient, cancellationToken);

        agent.ApplyStaticAssetUpdate(new StaticAssetUpdate(request.AssemblyName, request.RelativePath, request.Contents, request.IsApplicationProject));

        var logEntries = agent.GetAndClearLogEntries(request.ResponseLoggingLevel);

        // Updating static asset only invokes ContentUpdate metadata update handlers.
        // Failures of these handlers are reported to the log and ignored.
        // Therefore, this request always succeeds.
        var response = new UpdateResponse(logEntries, success: true);

        await response.WriteAsync(pipeClient, cancellationToken);
    }

    public static bool IsMatchingProcess(string processPath, string targetProcessPath)
    {
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var (shorter, longer) = (processPath.Length > targetProcessPath.Length) ? (targetProcessPath, processPath) : (processPath, targetProcessPath);

        // one or both have no extension, or they have the same extension
        if (longer.StartsWith(shorter, comparison))
        {
            var suffix = longer[shorter.Length..];
            return suffix is "" || suffix.Equals(".exe", comparison) || suffix.Equals(".dll", comparison);
        }

        // different extension:
        return (processPath.EndsWith(".exe", comparison) || processPath.EndsWith(".dll", comparison)) &&
               (targetProcessPath.EndsWith(".exe", comparison) || targetProcessPath.EndsWith(".dll", comparison)) &&
               string.Equals(processPath[..^4], targetProcessPath[..^4], comparison);
    }

    private static void Log(string message)
    {
        if (s_logToStandardOutput)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"dotnet watch 🕵️ [{s_namedPipeName}] {message}");
            Console.ResetColor();
        }
    }
}

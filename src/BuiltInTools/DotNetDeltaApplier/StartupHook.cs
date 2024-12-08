// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using Microsoft.DotNet.Watch;
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
    private static readonly string s_targetProcessPath = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.DotNetWatchHotReloadTargetProcessPath);

    /// <summary>
    /// Invoked by the runtime when the containing assembly is listed in DOTNET_STARTUP_HOOKS.
    /// </summary>
    public static void Initialize()
    {
        var processPath = Environment.GetCommandLineArgs().FirstOrDefault();

        // Workaround for https://github.com/dotnet/sdk/issues/40484
        // When launching the application process dotnet-watch sets Hot Reload environment variables via CLI environment directives (dotnet [env:X=Y] run).
        // Currently, the CLI parser sets the env variables to the dotnet.exe process itself, rather then to the target process.
        // This may cause the dotnet.exe process to connect to the named pipe and break it for the target process.
        //
        // Only needed when the agent is injected to the process by dotnet-watch, by the IDE.
        if (s_targetProcessPath != null && !IsMatchingProcess(processPath, s_targetProcessPath))
        {
            Log($"Ignoring process '{processPath}', expecting '{s_targetProcessPath}'");
            return;
        }

        Log($"Process: '{processPath}'");

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

        using var agent = new HotReloadAgent();
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

        var initPayload = new ClientInitializationRequest(agent.Capabilities);
        await initPayload.WriteAsync(pipeClient, cancellationToken);

        // Apply updates made before this process was launched to avoid executing unupdated versions of the affected modules.
        // The debugger takes care of this when launching process with the debugger attached.
        if (!Debugger.IsAttached)
        {
            await ReceiveAndApplyUpdatesAsync(pipeClient, agent, initialUpdates: true, cancellationToken);
        }
    }

    private static async Task ReceiveAndApplyUpdatesAsync(NamedPipeClientStream pipeClient, HotReloadAgent agent, bool initialUpdates, CancellationToken cancellationToken)
    {
        try
        {
            while (pipeClient.IsConnected)
            {
                var payloadType = (PayloadType)await pipeClient.ReadByteAsync(cancellationToken);
                switch (payloadType)
                {
                    case PayloadType.ManagedCodeUpdate:
                        await ReadAndApplyManagedUpdateAsync(pipeClient, agent, cancellationToken);
                        break;

                    case PayloadType.StaticAssetUpdate when !initialUpdates:
                        await ReadAndApplyStaticAssetUpdateAsync(pipeClient, agent, cancellationToken);
                        break;

                    case PayloadType.InitialUpdatesCompleted when initialUpdates:
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
        }
    }

    private static async Task ReadAndApplyManagedUpdateAsync(NamedPipeClientStream pipeClient, HotReloadAgent agent, CancellationToken cancellationToken)
    {
        var update = await UpdatePayload.ReadAsync(pipeClient, cancellationToken);

        try
        {
            agent.ApplyDeltas(update.Deltas);
        }
        catch (Exception e)
        {
            agent.Reporter.Report(e.ToString(), AgentMessageSeverity.Error);
        }

        var logEntries = agent.GetAndClearLogEntries(update.ResponseLoggingLevel);

        // response:
        pipeClient.WriteByte(UpdatePayload.ApplySuccessValue);
        await UpdatePayload.WriteLogAsync(pipeClient, logEntries, cancellationToken);
    }

    private static async ValueTask ReadAndApplyStaticAssetUpdateAsync(NamedPipeClientStream pipeClient, HotReloadAgent agent, CancellationToken cancellationToken)
    {
        var payload = await StaticAssetPayload.ReadAsync(pipeClient, default);
        try
        {
            //Log($"Attempting to apply static asset update for {update.RelativePath}.");

            agent.ApplyStaticAsset(payload.Update);

            ApplyResponsePayload result = new ApplyResponsePayload { ApplySucceeded = true };
            result.Write(pipeClient);
        }
        catch// (Exception ex)
        {
            // TODO:
            //var errMessage = ex.GetMessageFromException();
            //Log($"Update failed: {errMessage}");
            //    ApplyResponsePayload result = new ApplyResponsePayload { ApplySucceeded = false, ErrorString = errMessage };
            //    result.Write(pipeClient);
        }
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

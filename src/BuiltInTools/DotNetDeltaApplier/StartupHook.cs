// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using Microsoft.DotNet.Watch;
using Microsoft.DotNet.HotReload;

/// <summary>
/// The runtime startup hook looks for top-level type named "StartupHook".
/// </summary>
internal sealed class StartupHook
{
    private static readonly bool s_logToStandardOutput = Environment.GetEnvironmentVariable(EnvironmentVariableNames.HotReloadDeltaClientLogMessages) == "1";
    private static readonly string s_namedPipeName = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DotNetWatchHotReloadNamedPipeName);

    /// <summary>
    /// Invoked by the runtime when the containing assembly is listed in DOTNET_STARTUP_HOOKS.
    /// </summary>
    public static void Initialize()
    {
        var processPath = Environment.GetCommandLineArgs().FirstOrDefault();

        Log($"Loaded into process: {processPath}");

        HotReloadAgent.ClearHotReloadEnvironmentVariables(typeof(StartupHook));

        _ = Task.Run(async () =>
        {
            Log($"Connecting to hot-reload server");

            const int TimeOutMS = 5000;

            using var pipeClient = new NamedPipeClientStream(".", s_namedPipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
            try
            {
                await pipeClient.ConnectAsync(TimeOutMS);
                Log("Connected.");
            }
            catch (TimeoutException)
            {
                Log($"Failed to connect in {TimeOutMS}ms.");
                return;
            }

            using var agent = new HotReloadAgent();
            try
            {
                agent.Reporter.Report("Writing capabilities: " + agent.Capabilities, AgentMessageSeverity.Verbose);

                var initPayload = new ClientInitializationRequest(agent.Capabilities);
                await initPayload.WriteAsync(pipeClient, CancellationToken.None);

                while (pipeClient.IsConnected)
                {
                    var update = await ManagedCodeUpdateRequest.ReadAsync(pipeClient, CancellationToken.None);
                    Log($"ResponseLoggingLevel = {update.ResponseLoggingLevel}");

                    bool success;
                    try
                    {
                        agent.ApplyDeltas(update.Deltas);
                        success = true;
                    }
                    catch (Exception e)
                    {
                        agent.Reporter.Report($"The runtime failed to applying the change: {e.Message}", AgentMessageSeverity.Error);
                        agent.Reporter.Report("Further changes won't be applied to this process.", AgentMessageSeverity.Warning);
                        success = false;
                    }

                    var logEntries = agent.GetAndClearLogEntries(update.ResponseLoggingLevel);

                    var response = new UpdateResponse(logEntries, success);
                    await response.WriteAsync(pipeClient, CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }

            Log("Stopped received delta updates. Server is no longer connected.");
        });
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

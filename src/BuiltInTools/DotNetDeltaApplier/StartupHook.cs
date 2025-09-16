﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipes;
using Microsoft.DotNet.Watcher;
using Microsoft.Extensions.HotReload;

internal sealed class StartupHook
{
    private static readonly bool s_logToStandardOutput = Environment.GetEnvironmentVariable(EnvironmentVariables.Names.HotReloadDeltaClientLogMessages) == "1";
    private static readonly string s_namedPipeName = Environment.GetEnvironmentVariable(EnvironmentVariables.Names.DotnetWatchHotReloadNamedPipeName);
    private static readonly string s_targetProcessPath = Environment.GetEnvironmentVariable(EnvironmentVariables.Names.DotnetWatchHotReloadTargetProcessPath);

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
        if (!IsMatchingProcess(processPath, s_targetProcessPath))
        {
            Log($"Ignoring process '{processPath}', expecting '{s_targetProcessPath}'");
            return;
        }

        Log($"Loaded into process: {processPath}");

        ClearHotReloadEnvironmentVariables();

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

            using var agent = new HotReloadAgent(pipeClient, Log);
            try
            {
                await agent.ReceiveDeltasAsync();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
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

    internal static void ClearHotReloadEnvironmentVariables()
    {
        // Clear any hot-reload specific environment variables. This prevents child processes from being
        // affected by the current app's hot reload settings. See https://github.com/dotnet/runtime/issues/58000

        Environment.SetEnvironmentVariable(EnvironmentVariables.Names.DotnetStartupHooks,
            RemoveCurrentAssembly(Environment.GetEnvironmentVariable(EnvironmentVariables.Names.DotnetStartupHooks)));

        Environment.SetEnvironmentVariable(EnvironmentVariables.Names.DotnetWatchHotReloadNamedPipeName, "");
        Environment.SetEnvironmentVariable(EnvironmentVariables.Names.HotReloadDeltaClientLogMessages, "");
    }

    internal static string RemoveCurrentAssembly(string environment)
    {
        if (environment is "")
        {
            return environment;
        }

        var assemblyLocation = typeof(StartupHook).Assembly.Location;
        var updatedValues = environment.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(e => !string.Equals(e, assemblyLocation, StringComparison.OrdinalIgnoreCase));

        return string.Join(Path.PathSeparator, updatedValues);
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

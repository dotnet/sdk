// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using Microsoft.DotNet.HotReload;
using Microsoft.DotNet.Watch;

#if NET
using System.Runtime.Loader;
#endif

/// <summary>
/// The runtime startup hook looks for top-level type named "StartupHook".
/// </summary>
internal sealed class StartupHook
{
    private static readonly string? s_standardOutputLogPrefix = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.HotReloadDeltaClientLogMessages);
    private static readonly string? s_namedPipeName = Environment.GetEnvironmentVariable(AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName);

#if NET10_0_OR_GREATER
    private static PosixSignalRegistration? s_signalRegistration;
#endif

    /// <summary>
    /// Invoked by the runtime when the containing assembly is listed in DOTNET_STARTUP_HOOKS.
    /// </summary>
    public static void Initialize()
    {
        var processPath = Environment.GetCommandLineArgs().FirstOrDefault();
        var processDir = Path.GetDirectoryName(processPath)!;

        Log($"Loaded into process: {processPath} ({typeof(StartupHook).Assembly.Location})");

        HotReloadAgent.ClearHotReloadEnvironmentVariables(typeof(StartupHook));

        if (string.IsNullOrEmpty(s_namedPipeName))
        {
            Log($"Environment variable {AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName} has no value");
            return;
        }

        RegisterSignalHandlers();

        var agent = new HotReloadAgent(
#if NET
            assemblyResolvingHandler: (_, args) =>
            {
                Log($"Resolving '{args.Name}, Version={args.Version}'");
                var path = Path.Combine(processDir, args.Name + ".dll");
                return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
            }
#endif
        );

        var listener = new PipeListener(s_namedPipeName, agent, Log);

        // fire and forget:
        _ = listener.Listen(CancellationToken.None);
    }

    private static void RegisterSignalHandlers()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ProcessUtilities.EnableWindowsCtrlCHandling(Log);
        }
        else
        {
#if NET10_0_OR_GREATER
            // Register a handler for SIGTERM to allow graceful shutdown of the application on Unix.
            // See https://github.com/dotnet/docs/issues/46226.

            // Note: registered handlers are executed in reverse order of their registration.
            // Since the startup hook is executed before any code of the application, it is the first handler registered and thus the last to run.

            s_signalRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                Log($"SIGTERM received. Cancel={context.Cancel}");

                if (!context.Cancel)
                {
                    Environment.Exit(0);
                }
            });

            Log("Posix signal handlers registered.");
#endif
        }
    }

    private static void Log(string message)
    {
        var prefix = s_standardOutputLogPrefix;
        if (!string.IsNullOrEmpty(prefix))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine($"{prefix} {message}");
            Console.ResetColor();
        }
    }
}

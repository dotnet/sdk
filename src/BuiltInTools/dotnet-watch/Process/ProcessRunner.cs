// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch
{
    internal sealed class ProcessRunner(TimeSpan processCleanupTimeout)
    {
        private sealed class ProcessState
        {
            public int ProcessId;
            public bool HasExited;
        }

        // For testing purposes only, lock on access.
        private static readonly HashSet<int> s_runningApplicationProcesses = [];

        public static IReadOnlyCollection<int> GetRunningApplicationProcesses()
        {
            lock (s_runningApplicationProcesses)
            {
                return [.. s_runningApplicationProcesses];
            }
        }

        /// <summary>
        /// Launches a process.
        /// </summary>
        public async Task<int> RunAsync(ProcessSpec processSpec, ILogger logger, ProcessLaunchResult? launchResult, CancellationToken processTerminationToken)
        {
            var state = new ProcessState();
            var stopwatch = new Stopwatch();

            var onOutput = processSpec.OnOutput;

            using var process = CreateProcess(processSpec, onOutput, state, logger);

            stopwatch.Start();

            Exception? launchException = null;
            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Process can't be started.");
                }

                state.ProcessId = process.Id;

                if (processSpec.IsUserApplication)
                {
                    lock (s_runningApplicationProcesses)
                    {
                        s_runningApplicationProcesses.Add(state.ProcessId);
                    }
                }

                if (onOutput != null)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
            }
            catch (Exception e)
            {
                launchException = e;
            }

            var argsDisplay = processSpec.GetArgumentsDisplay();
            if (launchException == null)
            {
                logger.Log(MessageDescriptor.LaunchedProcess, processSpec.Executable, argsDisplay, state.ProcessId);
            }
            else
            {
                logger.Log(MessageDescriptor.FailedToLaunchProcess, processSpec.Executable, argsDisplay, launchException.Message);
                return int.MinValue;
            }

            if (launchResult != null)
            {
                launchResult.ProcessId = process.Id;
            }

            int? exitCode = null;

            try
            {
                try
                {
                    await process.WaitForExitAsync(processTerminationToken);
                }
                catch (OperationCanceledException)
                {
                    // Process termination requested via cancellation token.
                    // Either Ctrl+C was pressed or the process is being restarted.

                    // Non-cancellable to not leave orphaned processes around blocking resources:
                    await TerminateProcessAsync(process, processSpec, state, logger, CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (processSpec.IsUserApplication)
                {
                    logger.Log(MessageDescriptor.ApplicationFailed, e.Message);
                }
            }
            finally
            {
                stopwatch.Stop();

                if (processSpec.IsUserApplication)
                {
                    lock (s_runningApplicationProcesses)
                    {
                        s_runningApplicationProcesses.Remove(state.ProcessId);
                    }
                }

                state.HasExited = true;

                try
                {
                    exitCode = process.ExitCode;
                }
                catch
                {
                    exitCode = null;
                }

                logger.Log(MessageDescriptor.ProcessRunAndExited, process.Id, stopwatch.ElapsedMilliseconds, exitCode);

                if (processSpec.IsUserApplication)
                {
                    if (exitCode == 0)
                    {
                        logger.Log(MessageDescriptor.Exited);
                    }
                    else if (exitCode == null)
                    {
                        logger.Log(MessageDescriptor.ExitedWithUnknownErrorCode);
                    }
                    else
                    {
                        logger.Log(MessageDescriptor.ExitedWithErrorCode, exitCode);
                    }
                }

                if (processSpec.OnExit != null)
                {
                    await processSpec.OnExit(state.ProcessId, exitCode);
                }
            }

            return exitCode ?? int.MinValue;
        }

        private static Process CreateProcess(ProcessSpec processSpec, Action<OutputLine>? onOutput, ProcessState state, ILogger logger)
        {
            var process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo =
                {
                    FileName = processSpec.Executable,
                    UseShellExecute = false,
                    WorkingDirectory = processSpec.WorkingDirectory,
                    RedirectStandardOutput = onOutput != null,
                    RedirectStandardError = onOutput != null,
                }
            };

            if (processSpec.IsUserApplication && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process.StartInfo.CreateNewProcessGroup = true;
            }

            if (processSpec.EscapedArguments is not null)
            {
                process.StartInfo.Arguments = processSpec.EscapedArguments;
            }
            else if (processSpec.Arguments is not null)
            {
                for (var i = 0; i < processSpec.Arguments.Count; i++)
                {
                    process.StartInfo.ArgumentList.Add(processSpec.Arguments[i]);
                }
            }

            foreach (var env in processSpec.EnvironmentVariables)
            {
                process.StartInfo.Environment.Add(env.Key, env.Value);
            }

            if (onOutput != null)
            {
                process.OutputDataReceived += (_, args) =>
                {
                    try
                    {
                        if (args.Data != null)
                        {
                            onOutput(new OutputLine(args.Data, IsError: false));
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(MessageDescriptor.ErrorReadingProcessOutput, "stdout", state.ProcessId, e.Message);
                    }
                };

                process.ErrorDataReceived += (_, args) =>
                {
                    try
                    {
                        if (args.Data != null)
                        {
                            onOutput(new OutputLine(args.Data, IsError: true));
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(MessageDescriptor.ErrorReadingProcessOutput, "stderr", state.ProcessId, e.Message);
                    }
                };
            }

            return process;
        }

        private async ValueTask TerminateProcessAsync(Process process, ProcessSpec processSpec, ProcessState state, ILogger logger, CancellationToken cancellationToken)
        {
            var forceOnly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !processSpec.IsUserApplication;

            // Ctrl+C hasn't been sent.
            TerminateProcess(process, state, logger, forceOnly);

            if (forceOnly)
            {
                _ = await WaitForExitAsync(process, state, timeout: null, logger, cancellationToken);
                return;
            }

            // Ctlr+C/SIGTERM has been sent, wait for the process to exit gracefully.
            if (processCleanupTimeout.TotalMilliseconds == 0 ||
                !await WaitForExitAsync(process, state, processCleanupTimeout, logger, cancellationToken))
            {
                // Force termination if the process is still running after the timeout.
                TerminateProcess(process, state, logger, force: true);

                _ = await WaitForExitAsync(process, state, timeout: null, logger, cancellationToken);
            }
        }

        private static async ValueTask<bool> WaitForExitAsync(Process process, ProcessState state, TimeSpan? timeout, ILogger logger, CancellationToken cancellationToken)
        {
            // On Linux simple call WaitForExitAsync does not work reliably (it may hang).
            // As a workaround we poll for HasExited.
            // See also https://github.com/dotnet/runtime/issues/109434.

            var task = process.WaitForExitAsync(cancellationToken);

            if (timeout is { } timeoutValue)
            {
                try
                {
                    logger.Log(MessageDescriptor.WaitingForProcessToExitWithin, state.ProcessId, timeoutValue.TotalSeconds);
                    await task.WaitAsync(timeoutValue, cancellationToken);
                }
                catch (TimeoutException)
                {
                    try
                    {
                        return process.HasExited;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            else
            {
                int i = 1;
                while (true)
                {
                    try
                    {
                        if (process.HasExited)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }

                    logger.Log(MessageDescriptor.WaitingForProcessToExit, state.ProcessId, i++);

                    try
                    {
                        await task.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                        break;
                    }
                    catch (TimeoutException)
                    {
                    }
                }
            }

            return true;
        }

        private static void TerminateProcess(Process process, ProcessState state, ILogger logger, bool force)
        {
            try
            {
                if (!state.HasExited && !process.HasExited)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        TerminateWindowsProcess(process, state, logger, force);
                    }
                    else
                    {
                        TerminateUnixProcess(state, logger, force);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Log(MessageDescriptor.FailedToKillProcess, state.ProcessId, e.Message);
            }
        }

        private static void TerminateWindowsProcess(Process process, ProcessState state, ILogger logger, bool force)
        {
            var signalName = force ? "Kill" : "Ctrl+C";
            logger.Log(MessageDescriptor.TerminatingProcess, state.ProcessId, signalName);

            if (force)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception e)
                {
                    logger.Log(MessageDescriptor.FailedToSendSignalToProcess, signalName, state.ProcessId, e.Message);
                }
            }
            else
            {
                var error = ProcessUtilities.SendWindowsCtrlCEvent(state.ProcessId);
                if (error != null)
                {
                    logger.Log(MessageDescriptor.FailedToSendSignalToProcess, signalName, state.ProcessId, error);
                }
            }
        }

        private static void TerminateUnixProcess(ProcessState state, ILogger logger, bool force)
        {
            var signalName = force ? "SIGKILL" : "SIGTERM";
            logger.Log(MessageDescriptor.TerminatingProcess, state.ProcessId, signalName);

            var error = ProcessUtilities.SendPosixSignal(state.ProcessId, signal: force ? ProcessUtilities.SIGKILL : ProcessUtilities.SIGTERM);
            if (error != null)
            {
                logger.Log(MessageDescriptor.FailedToSendSignalToProcess, signalName, state.ProcessId, error);
            }
        }
    }
}

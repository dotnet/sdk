// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.DotNet.HotReload;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal class ProcessRunner(TimeSpan processCleanupTimeout)
{
    // For testing purposes only, lock on access.
    private static readonly HashSet<int> s_runningApplicationProcesses = [];

    // Exit code used by the OS when process is terminated by an external signal.
    private static readonly int s_processTerminatedExitCode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? unchecked((int)0xC000013A) : 137;

    public static IReadOnlyCollection<int> GetRunningApplicationProcesses()
    {
        lock (s_runningApplicationProcesses)
        {
            return [.. s_runningApplicationProcesses];
        }
    }

    /// <summary>
    /// Launches a process.
    /// Virtual for testing.
    /// </summary>
    public virtual async Task<int> RunAsync(ProcessSpec processSpec, ILogger logger, ProcessLaunchResult? launchResult, CancellationToken processTerminationToken)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        using var state = TryStartProcessImpl(processSpec, logger);
        if (state == null)
        {
            return int.MinValue;
        }

        if (processSpec.IsUserApplication)
        {
            lock (s_runningApplicationProcesses)
            {
                s_runningApplicationProcesses.Add(state.ProcessId);
            }
        }

        launchResult?.ProcessId = state.ProcessId;

        int? exitCode = null;

        try
        {
            try
            {
                await state.Process.WaitForExitAsync(processTerminationToken);
            }
            catch (OperationCanceledException)
            {
                // Process termination requested via cancellation token.
                // Either Ctrl+C was pressed or the process is being restarted.

                // Non-cancellable to not leave orphaned processes around blocking resources:
                await state.TerminateProcessAsync(processCleanupTimeout, logger);
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

            state.Exited();

            try
            {
                exitCode = state.Process.ExitCode;
            }
            catch
            {
                exitCode = null;
            }

            logger.Log(MessageDescriptor.ProcessRunAndExited, state.ProcessId, stopwatch.ElapsedMilliseconds, exitCode);

            if (processSpec.IsUserApplication)
            {
                if (exitCode == 0 || exitCode == s_processTerminatedExitCode)
                {
                    logger.Log(MessageDescriptor.Exited);
                }
                else if (exitCode == null)
                {
                    logger.Log(MessageDescriptor.ExitedWithUnknownErrorCode);
                }
                else
                {
                    logger.Log(MessageDescriptor.ExitedWithErrorCode, exitCode.Value);
                }
            }

            if (processSpec.OnExit != null)
            {
                await processSpec.OnExit(state.ProcessId, exitCode);
            }
        }

        return exitCode ?? int.MinValue;
    }

    internal static Process? TryStartProcess(ProcessSpec processSpec, ILogger logger)
        => TryStartProcessImpl(processSpec, logger)?.Process;

    private static ProcessState? TryStartProcessImpl(ProcessSpec processSpec, ILogger logger)
    {
        var onOutput = processSpec.OnOutput;

        var process = new Process
        {
            EnableRaisingEvents = true,
            StartInfo =
            {
                FileName = processSpec.Executable,
                UseShellExecute = processSpec.UseShellExecute,
                WorkingDirectory = processSpec.WorkingDirectory,
                RedirectStandardOutput = onOutput != null,
                RedirectStandardError = onOutput != null,
            }
        };

        var state = new ProcessState(process, processSpec.IsUserApplication);

        if (processSpec.IsUserApplication && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            process.StartInfo.CreateNewProcessGroup = true;
        }

        for (var i = 0; i < processSpec.Arguments.Count; i++)
        {
            process.StartInfo.ArgumentList.Add(processSpec.Arguments[i]);
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

        var argsDisplay = processSpec.GetArgumentsDisplay();

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Process can't be started.");
            }

            state.Started(process.Id);

            if (onOutput != null)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            logger.Log(MessageDescriptor.LaunchedProcess, processSpec.Executable, argsDisplay, state.ProcessId);
            return state;
        }
        catch (Exception e)
        {
            logger.Log(MessageDescriptor.FailedToLaunchProcess, processSpec.Executable, argsDisplay, e.Message);

            state.Dispose();
            return null;
        }
    }
}

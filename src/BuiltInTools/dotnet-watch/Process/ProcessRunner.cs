// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;

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
        public async Task<int> RunAsync(ProcessSpec processSpec, IReporter reporter, ProcessLaunchResult? launchResult, CancellationToken processTerminationToken)
        {
            var state = new ProcessState();
            var stopwatch = new Stopwatch();

            var onOutput = processSpec.OnOutput;

            // If output isn't already redirected (build invocation) we redirect it to the reporter.
            // The reporter synchronizes the output of the process with the reporter output,
            // so that the printed lines don't interleave.
            onOutput ??= line => reporter.ReportProcessOutput(line);

            using var process = CreateProcess(processSpec, onOutput, state, reporter);

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
                reporter.Report(MessageDescriptor.LaunchedProcess, [processSpec.Executable, argsDisplay, state.ProcessId]);
            }
            else
            {
                reporter.Error($"Failed to launch '{processSpec.Executable}' with arguments '{argsDisplay}': {launchException.Message}");
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
                    await TerminateProcessAsync(process, processSpec, state, reporter, CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (processSpec.IsUserApplication)
                {
                    reporter.Error($"Application failed: {e.Message}");
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

                reporter.Verbose($"Process id {process.Id} ran for {stopwatch.ElapsedMilliseconds}ms and exited with exit code {exitCode}.");

                if (processSpec.IsUserApplication)
                {
                    if (exitCode == 0)
                    {
                        reporter.Report(MessageDescriptor.Exited);
                    }
                    else if (exitCode == null)
                    {
                        reporter.Report(MessageDescriptor.ExitedWithUnknownErrorCode);
                    }
                    else
                    {
                        reporter.Report(MessageDescriptor.ExitedWithErrorCode, exitCode);
                    }
                }

                if (processSpec.OnExit != null)
                {
                    await processSpec.OnExit(state.ProcessId, exitCode);
                }
            }

            return exitCode ?? int.MinValue;
        }

        private static Process CreateProcess(ProcessSpec processSpec, Action<OutputLine>? onOutput, ProcessState state, IReporter reporter)
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
                        reporter.Verbose($"Error reading stdout of process {state.ProcessId}: {e}");
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
                        reporter.Verbose($"Error reading stderr of process {state.ProcessId}: {e}");
                    }
                };
            }

            return process;
        }

        private async ValueTask TerminateProcessAsync(Process process, ProcessSpec processSpec, ProcessState state, IReporter reporter, CancellationToken cancellationToken)
        {
            var forceOnly = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !processSpec.IsUserApplication;

            // Ctrl+C hasn't been sent.
            TerminateProcess(process, state, reporter, forceOnly);

            if (forceOnly)
            {
                _ = await WaitForExitAsync(process, state, timeout: null, reporter, cancellationToken);
                return;
            }

            // Ctlr+C/SIGTERM has been sent, wait for the process to exit gracefully.
            if (processCleanupTimeout.TotalMilliseconds == 0 ||
                !await WaitForExitAsync(process, state, processCleanupTimeout, reporter, cancellationToken))
            {
                // Force termination if the process is still running after the timeout.
                TerminateProcess(process, state, reporter, force: true);

                _ = await WaitForExitAsync(process, state, timeout: null, reporter, cancellationToken);
            }
        }

        private static async ValueTask<bool> WaitForExitAsync(Process process, ProcessState state, TimeSpan? timeout, IReporter reporter, CancellationToken cancellationToken)
        {
            // On Linux simple call WaitForExitAsync does not work reliably (it may hang).
            // As a workaround we poll for HasExited.
            // See also https://github.com/dotnet/runtime/issues/109434.

            var task = process.WaitForExitAsync(cancellationToken);

            if (timeout is { } timeoutValue)
            {
                try
                {
                    reporter.Verbose($"Waiting for process {state.ProcessId} to exit within {timeoutValue.TotalSeconds}s.");
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

                    reporter.Verbose($"Waiting for process {state.ProcessId} to exit ({i++}).");

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

        private static void TerminateProcess(Process process, ProcessState state, IReporter reporter, bool force)
        {
            try
            {
                if (!state.HasExited && !process.HasExited)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        TerminateWindowsProcess(process, state, reporter, force);
                    }
                    else
                    {
                        TerminateUnixProcess(state, reporter, force);
                    }
                }
            }
            catch (Exception ex)
            {
                reporter.Verbose($"Error while killing process {state.ProcessId}: {ex.Message}");
#if DEBUG
                reporter.Verbose(ex.ToString());
#endif
            }
        }

        private static void TerminateWindowsProcess(Process process, ProcessState state, IReporter reporter, bool force)
        {
            var processId = state.ProcessId;

            reporter.Verbose($"Terminating process {processId} ({(force ? "Kill" : "Ctrl+C")}).");

            if (force)
            {
                process.Kill();
            }
            else
            {
                ProcessUtilities.SendWindowsCtrlCEvent(processId, m => reporter.Verbose(m));
            }
        }

        private static void TerminateUnixProcess(ProcessState state, IReporter reporter, bool force)
        {
            reporter.Verbose($"Terminating process {state.ProcessId} ({(force ? "SIGKILL" : "SIGTERM")}).");

            ProcessUtilities.SendPosixSignal(
                state.ProcessId,
                signal: force ? ProcessUtilities.SIGKILL : ProcessUtilities.SIGTERM,
                log: m => reporter.Verbose(m));
        }
    }
}

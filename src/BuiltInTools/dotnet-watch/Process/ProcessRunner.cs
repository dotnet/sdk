// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;

namespace Microsoft.DotNet.Watch
{
    internal sealed class ProcessRunner(
        TimeSpan processCleanupTimeout,
        CancellationToken shutdownCancellationToken)
    {
        private const int SIGKILL = 9;
        private const int SIGTERM = 15;

        private sealed class ProcessState
        {
            public int ProcessId;
            public bool HasExited;
        }

        /// <summary>
        /// Launches a process.
        /// </summary>
        /// <param name="isUserApplication">True if the process is a user application, false if it is a helper process (e.g. msbuild).</param>
        public async Task<int> RunAsync(ProcessSpec processSpec, IReporter reporter, bool isUserApplication, ProcessLaunchResult? launchResult, CancellationToken processTerminationToken)
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
                reporter.Report(MessageDescriptor.LaunchedProcess, processSpec.Executable, argsDisplay, state.ProcessId);
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
                    await TerminateProcessAsync(process, state, reporter, CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (isUserApplication)
                {
                    reporter.Error($"Application failed: {e.Message}");
                }
            }
            finally
            {
                stopwatch.Stop();

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

                if (isUserApplication)
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

        private async ValueTask TerminateProcessAsync(Process process, ProcessState state, IReporter reporter, CancellationToken cancellationToken)
        {
            if (!shutdownCancellationToken.IsCancellationRequested)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Ctrl+C hasn't been sent, force termination.
                    // We don't have means to terminate gracefully on Windows (https://github.com/dotnet/runtime/issues/109432)
                    TerminateProcess(process, state, reporter, force: true);
                    _ = await WaitForExitAsync(process, state, timeout: null, reporter, cancellationToken);

                    return;
                }
                else
                {
                    // Ctrl+C hasn't been sent, send SIGTERM now:
                    TerminateProcess(process, state, reporter, force: false);
                }
            }

            // Ctlr+C/SIGTERM has been sent, wait for the process to exit gracefully.
            if (processCleanupTimeout.Milliseconds == 0 ||
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
            // Needs API: https://github.com/dotnet/runtime/issues/109432
            // Code below does not work because the process creation needs CREATE_NEW_PROCESS_GROUP flag.

            reporter.Verbose($"Terminating process {state.ProcessId}.");

            if (force)
            {
                process.Kill();
            }
#if TODO
            else
            {
                const uint CTRL_C_EVENT = 0;

                [DllImport("kernel32.dll", SetLastError = true)]
                static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

                [DllImport("kernel32.dll", SetLastError = true)]
                static extern bool AttachConsole(uint dwProcessId);

                [DllImport("kernel32.dll", SetLastError = true)]
                static extern bool FreeConsole();

                if (AttachConsole((uint)state.ProcessId) &&
                    GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0) &&
                    FreeConsole())
                {
                    return;
                }

                var error = Marshal.GetLastPInvokeError();
                reporter.Verbose($"Failed to send Ctrl+C to process {state.ProcessId}: {Marshal.GetPInvokeErrorMessage(error)} (code {error})");
            }
#endif
        }

        private static void TerminateUnixProcess(ProcessState state, IReporter reporter, bool force)
        {
            [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
            static extern int sys_kill(int pid, int sig);

            reporter.Verbose($"Terminating process {state.ProcessId} ({(force ? "SIGKILL" : "SIGTERM")}).");

            var result = sys_kill(state.ProcessId, force ? SIGKILL : SIGTERM);
            if (result != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                reporter.Verbose($"Error while sending SIGTERM to process {state.ProcessId}: {Marshal.GetPInvokeErrorMessage(error)} (code {error}).");
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;

namespace Microsoft.DotNet.Watch
{
    internal sealed class ProcessRunner
    {
        private const int SIGKILL = 9;
        private const int SIGTERM = 15;

        private sealed class ProcessState
        {
            public int ProcessId;
            public bool HasExited;
            public bool ForceExit;
        }

        /// <summary>
        /// Launches a process.
        /// </summary>
        /// <param name="isUserApplication">True if the process is a user application, false if it is a helper process (e.g. msbuild).</param>
        public static async Task<int> RunAsync(ProcessSpec processSpec, IReporter reporter, bool isUserApplication, ProcessLaunchResult? launchResult, CancellationToken processTerminationToken)
        {
            Ensure.NotNull(processSpec, nameof(processSpec));

            var state = new ProcessState();
            var stopwatch = new Stopwatch();

            var onOutput = processSpec.OnOutput;

            // allow tests to watch for application output:
            if (reporter.EnableProcessOutputReporting)
            {
                onOutput += line => reporter.ReportProcessOutput(line);
            }

            using var process = CreateProcess(processSpec, onOutput, state, reporter);

            processTerminationToken.Register(() => TerminateProcess(process, state, reporter));

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
                    // Wait for the actual process exit.
                    while (true)
                    {
                        try
                        {
                            // non-cancellable to not leave orphaned processes around blocking resources:
                            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                            break;
                        }
                        catch (TimeoutException)
                        {
                            // nop
                        }

                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || state.ForceExit)
                        {
                            reporter.Output($"Waiting for process {state.ProcessId} to exit ...");
                        }
                        else
                        {
                            reporter.Output($"Forcing process {state.ProcessId} to exit ...");
                        }

                        state.ForceExit = true;
                    }
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
                        reporter.Output("Exited");
                    }
                    else if (exitCode == null)
                    {
                        reporter.Error("Exited with unknown error code");
                    }
                    else
                    {
                        reporter.Error($"Exited with error code {exitCode}");
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
                    RedirectStandardOutput =  onOutput != null,
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

        private static void TerminateProcess(Process process, ProcessState state, IReporter reporter)
        {
            try
            {
                if (!state.HasExited && !process.HasExited)
                {
                    reporter.Report(MessageDescriptor.KillingProcess, state.ProcessId.ToString());

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        TerminateWindowsProcess(process, state, reporter);
                    }
                    else
                    {
                        TerminateUnixProcess(state, reporter);
                    }

                    reporter.Verbose($"Process {state.ProcessId} killed.");
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

        private static void TerminateWindowsProcess(Process process, ProcessState state, IReporter reporter)
        {
            // Needs API: https://github.com/dotnet/runtime/issues/109432
            // Code below does not work because the process creation needs CREATE_NEW_PROCESS_GROUP flag.
#if TODO    
            if (!state.ForceExit)
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

            process.Kill();
        }

        private static void TerminateUnixProcess(ProcessState state, IReporter reporter)
        {
            [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
            static extern int sys_kill(int pid, int sig);

            var result = sys_kill(state.ProcessId, state.ForceExit ? SIGKILL : SIGTERM);
            if (result != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                reporter.Verbose($"Error while sending SIGTERM to process {state.ProcessId}: {Marshal.GetPInvokeErrorMessage(error)} (code {error}).");
            }
        }
    }
}

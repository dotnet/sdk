// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class ProcessRunner
    {
        /// <summary>
        /// Launches a process.
        /// </summary>
        /// <param name="isUserApplication">True if the process is a user application, false if it is a helper process (e.g. msbuild).</param>
        public static async Task<int> RunAsync(ProcessSpec processSpec, IReporter reporter, bool isUserApplication, ProcessLaunchResult? launchResult, CancellationToken processTerminationToken)
        {
            Ensure.NotNull(processSpec, nameof(processSpec));

            var stopwatch = new Stopwatch();

            var onOutput = processSpec.OnOutput;

            // allow tests to watch for application output:
            if (reporter.EnableProcessOutputReporting)
            {
                onOutput += line => reporter.ReportProcessOutput(line);
            }

            using var process = CreateProcess(processSpec, redirectOutput: onOutput != null);

            if (onOutput != null)
            {
                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                    {
                        onOutput(new OutputLine(args.Data, IsError: false));
                    }
                };

                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                    {
                        onOutput(new OutputLine(args.Data, IsError: true));
                    }
                };
            }

            using var processState = new ProcessState(process, reporter, processSpec.TerminateEntireProcessTree);
            processTerminationToken.Register(() => processState.TryKill());

            stopwatch.Start();

            int? processId = null;
            try
            {
                if (process.Start())
                {
                    processId = process.Id;
                    processState.ProcessId = processId;

                    if (launchResult != null)
                    {
                        launchResult.ProcessId = processId;
                    }
                }
            }
            finally
            {
                var argsDisplay = processSpec.GetArgumentsDisplay();

                if (processId.HasValue)
                {
                    reporter.Report(MessageDescriptor.LaunchedProcess, processSpec.Executable, argsDisplay, processId.Value);
                }
                else
                {
                    reporter.Error($"Failed to launch '{processSpec.Executable}' with arguments '{argsDisplay}'");
                }
            }

            if (processId == null)
            {
                // failed to launch
                return int.MinValue;
            }

            if (onOutput != null)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            int? exitCode = null;
            var failed = false;

            try
            {
                await processState.Task;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failed = true;

                if (isUserApplication)
                {
                    reporter.Error($"Application failed to launch: {e.Message}");
                }
            }
            finally
            {
                stopwatch.Stop();

                if (!failed && !processTerminationToken.IsCancellationRequested)
                {
                    try
                    {
                        exitCode = process.ExitCode;
                    }
                    catch
                    {
                        exitCode = null;
                    }

                    reporter.Verbose($"Process id {process.Id} ran for {stopwatch.ElapsedMilliseconds}ms.");

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
                }

                Debug.Assert(processId != null);
                if (processSpec.OnExit != null)
                {
                    await processSpec.OnExit(processId.Value, exitCode);
                }
            }

            return exitCode ?? int.MinValue;
        }

        private static Process CreateProcess(ProcessSpec processSpec, bool redirectOutput)
        {
            var process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo =
                {
                    FileName = processSpec.Executable,
                    UseShellExecute = false,
                    WorkingDirectory = processSpec.WorkingDirectory,
                    RedirectStandardOutput =  redirectOutput,
                    RedirectStandardError = redirectOutput,
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

            return process;
        }

        private sealed class ProcessState : IDisposable
        {
            private readonly IReporter _reporter;
            private readonly bool _terminateEntireProcessTree;
            private readonly Process _process;
            private readonly TaskCompletionSource _processExitedCompletionSource = new();
            private volatile bool _disposed;

            public readonly Task Task;
            public int? ProcessId;

            public ProcessState(Process process, IReporter reporter, bool terminateEntireProcessTree)
            {
                _reporter = reporter;
                _terminateEntireProcessTree = terminateEntireProcessTree;
                _process = process;
                _process.Exited += OnExited;

                Task = _processExitedCompletionSource.Task.ContinueWith(_ =>
                {
                    try
                    {
                        // We need to use two WaitForExit calls to ensure that all of the output/events are processed. Previously
                        // this code used Process.Exited, which could result in us missing some output due to the ordering of
                        // events.
                        //
                        // See the remarks here: https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit#System_Diagnostics_Process_WaitForExit_System_Int32_
                        if (!_process.WaitForExit(int.MaxValue))
                        {
                            throw new TimeoutException();
                        }

                        _process.WaitForExit();
                    }
                    catch (InvalidOperationException)
                    {
                        // suppress if this throws if no process is associated with this object anymore.
                    }
                });
            }

            public void TryKill()
            {
                if (_disposed)
                {
                    return;
                }

                var processIdDisplay = ProcessId?.ToString() ?? "<unknown>";

                try
                {
                    if (!_process.HasExited)
                    {
                        _reporter.Report(_terminateEntireProcessTree ? MessageDescriptor.KillingProcessTree : MessageDescriptor.KillingProcess, processIdDisplay);
                        _process.Kill(_terminateEntireProcessTree);
                        _reporter.Verbose($"Process {processIdDisplay} killed.");
                    }
                }
                catch (Exception ex)
                {
                    _reporter.Verbose($"Error while killing process {processIdDisplay} '{_process.StartInfo.FileName} {_process.StartInfo.Arguments}': {ex.Message}");
#if DEBUG
                    _reporter.Verbose(ex.ToString());
#endif
                }
            }

            private void OnExited(object? sender, EventArgs args)
                => _processExitedCompletionSource.TrySetResult();

            public void Dispose()
            {
                if (!_disposed)
                {
                    TryKill();
                    _disposed = true;
                    _process.Exited -= OnExited;
                    _process.Dispose();
                }
            }
        }
    }
}

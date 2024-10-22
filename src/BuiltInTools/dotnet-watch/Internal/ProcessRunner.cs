// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
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

            int? processId = null;
            using var process = CreateProcess(processSpec, redirectOutput: onOutput != null);

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
                        reporter.Verbose($"Error reading stdout of process {processId}: {e}");
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
                        reporter.Verbose($"Error reading stderr of process {processId}: {e}");
                    }
                };
            }

            processTerminationToken.Register(() => TerminateProcess(process, processId, reporter, processSpec.TerminateEntireProcessTree));

            stopwatch.Start();
            try
            {
                if (process.Start())
                {
                    processId = process.Id;
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
                // non-cancellable to not leave orphaned processes around:
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failed = true;

                if (isUserApplication)
                {
                    reporter.Error($"Application failed: {e.Message}");
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

        private static void TerminateProcess(Process process, int? processId, IReporter reporter, bool terminateEntireProcessTree)
        {
            var processIdDisplay = processId?.ToString() ?? "<unknown>";

            try
            {
                if (!process.HasExited)
                {
                    reporter.Report(terminateEntireProcessTree ? MessageDescriptor.KillingProcessTree : MessageDescriptor.KillingProcess, processIdDisplay);
                    process.Kill(terminateEntireProcessTree);
                    reporter.Verbose($"Process {processIdDisplay} killed.");
                }
            }
            catch (Exception ex)
            {
                reporter.Verbose($"Error while killing process {processIdDisplay}: {ex.Message}");
#if DEBUG
                reporter.Verbose(ex.ToString());
#endif
            }
        }
    }
}

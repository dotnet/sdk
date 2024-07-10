﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using Microsoft.Extensions.Tools.Internal;
using IReporter = Microsoft.Extensions.Tools.Internal.IReporter;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class ProcessRunner(IReporter reporter)
    {
        // May not be necessary in the future. See https://github.com/dotnet/corefx/issues/12039
        public async Task<int> RunAsync(ProcessSpec processSpec, CancellationToken cancellationToken)
        {
            Ensure.NotNull(processSpec, nameof(processSpec));

            int exitCode;

            var stopwatch = new Stopwatch();

            using var process = CreateProcess(processSpec);
            using var processState = new ProcessState(process, reporter);

            cancellationToken.Register(() => processState.TryKill());

            var readOutput = false;
            var readError = false;
            if (processSpec.IsOutputCaptured)
            {
                readOutput = true;
                readError = true;
                process.OutputDataReceived += (_, a) =>
                {
                    if (!string.IsNullOrEmpty(a.Data))
                    {
                        processSpec.OutputCapture.AddLine(a.Data);
                    }
                };
                process.ErrorDataReceived += (_, a) =>
                {
                    if (!string.IsNullOrEmpty(a.Data))
                    {
                        processSpec.OutputCapture.AddLine(a.Data);
                    }
                };
            }
            else if (processSpec.OnOutput != null)
            {
                readOutput = true;
                process.OutputDataReceived += processSpec.OnOutput;
            }

            stopwatch.Start();

            int? processId = null;
            try
            {
                if (process.Start())
                {
                    processId = process.Id;
                }
            }
            finally
            {
                var argsDisplay = processSpec.GetArgumentsDisplay();

                if (processId.HasValue)
                {
                    reporter.Verbose($"Launched '{processSpec.Executable}' with arguments '{argsDisplay}': process id {processId.Value}", emoji: "🚀");
                }
                else
                {
                    reporter.Verbose($"Failed to launch '{processSpec.Executable}' with arguments '{argsDisplay}'");
                }
            }

            if (readOutput)
            {
                process.BeginOutputReadLine();
            }
            if (readError)
            {
                process.BeginErrorReadLine();
            }

            await processState.Task;

            exitCode = process.ExitCode;
            stopwatch.Stop();
            reporter.Verbose($"Process id {process.Id} ran for {stopwatch.ElapsedMilliseconds}ms");

            return exitCode;
        }

        private static Process CreateProcess(ProcessSpec processSpec)
        {
            var process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo =
                {
                    FileName = processSpec.Executable,
                    UseShellExecute = false,
                    WorkingDirectory = processSpec.WorkingDirectory,
                    RedirectStandardOutput = processSpec.IsOutputCaptured || (processSpec.OnOutput != null),
                    RedirectStandardError = processSpec.IsOutputCaptured,
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
            private readonly Process _process;
            private readonly TaskCompletionSource _tcs = new();
            private volatile bool _disposed;

            public ProcessState(Process process, IReporter reporter)
            {
                _reporter = reporter;
                _process = process;
                _process.Exited += OnExited;
                Task = _tcs.Task.ContinueWith(_ =>
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

            public Task Task { get; }

            public void TryKill()
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    if (_process is not null && !_process.HasExited)
                    {
                        _reporter.Verbose($"Killing process {_process.Id}");
                        _process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    _reporter.Verbose($"Error while killing process '{_process.StartInfo.FileName} {_process.StartInfo.Arguments}': {ex.Message}");
#if DEBUG
                    _reporter.Verbose(ex.ToString());
#endif
                }
            }

            private void OnExited(object? sender, EventArgs args)
                => _tcs.TrySetResult();

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

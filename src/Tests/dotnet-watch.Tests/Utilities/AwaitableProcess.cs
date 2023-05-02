// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class AwaitableProcess : IDisposable
    {
        private readonly object _testOutputLock = new object();

        private Process _process;
        private readonly DotnetCommand _spec;
        private readonly List<string> _lines;
        private BufferBlock<string> _source;
        private ITestOutputHelper _logger;
        private TaskCompletionSource<int> _exited;
        private bool _disposed;

        public AwaitableProcess(DotnetCommand spec, ITestOutputHelper logger)
        {
            _spec = spec;
            _logger = logger;
            _source = new BufferBlock<string>();
            _lines = new List<string>();
            _exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public IEnumerable<string> Output => _lines;

        public Task Exited => _exited.Task;

        public int Id => _process.Id;

        public Process Process => _process;

        public void Start()
        {
            if (_process != null)
            {
                throw new InvalidOperationException("Already started");
            }

            var processStartInfo = _spec.GetProcessStartInfo();
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.StandardOutputEncoding = Encoding.UTF8;
            processStartInfo.StandardErrorEncoding = Encoding.UTF8;

            _process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo,
            };

            _process.OutputDataReceived += OnData;
            _process.ErrorDataReceived += OnData;
            _process.Exited += OnExit;

            WriteTestOutput($"{DateTime.Now}: starting process: '{_process.StartInfo.FileName} {_process.StartInfo.Arguments}'");
            _process.Start();
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();
            WriteTestOutput($"{DateTime.Now}: process started: '{_process.StartInfo.FileName} {_process.StartInfo.Arguments}'");
        }

        public async Task<string> GetOutputLineAsync(Predicate<string> success, Predicate<string> failure)
        {
            bool failed = false;

            using var cancellationOnFailure = new CancellationTokenSource();

            while (!_source.Completion.IsCompleted && !failed)
            {
                try
                {
                    while (await _source.OutputAvailableAsync(cancellationOnFailure.Token))
                    {
                        var line = await _source.ReceiveAsync(cancellationOnFailure.Token);
                        _lines.Add(line);
                        if (success(line))
                        {
                            return line;
                        }

                        if (failure(line))
                        {
                            failed = true;

                            // Limit the time to collect remaining output after a failure to avoid hangs:
                            cancellationOnFailure.CancelAfter(TimeSpan.FromSeconds(1));
                        }
                    }
                }
                catch (OperationCanceledException) when (failed)
                {
                    break;
                }
            }

            return null;
        }

        public async Task<IList<string>> GetAllOutputLinesAsync(CancellationToken cancellationToken)
        {
            var lines = new List<string>();
            while (!_source.Completion.IsCompleted)
            {
                while (await _source.OutputAvailableAsync(cancellationToken))
                {
                    lines.Add(await _source.ReceiveAsync(cancellationToken));
                }
            }
            return lines;
        }

        private void OnData(object sender, DataReceivedEventArgs args)
        {
            var line = args.Data ?? string.Empty;

            WriteTestOutput($"{DateTime.Now}: post: '{line}'");
            _source.Post(line);
        }

        private void WriteTestOutput(string text)
        {
            lock (_testOutputLock)
            {
                if (!_disposed)
                {
                    _logger.WriteLine(text);
                }
            }
        }

        private void OnExit(object sender, EventArgs args)
        {
            // Wait to ensure the process has exited and all output consumed
            _process.WaitForExit();
            _source.Complete();
            _exited.TrySetResult(_process.ExitCode);

            try
            {
                WriteTestOutput($"Process {_process.Id} has exited");
            }
            catch
            {
                // test might not be running anymore
            }
        }

        public void Dispose()
        {
            _source.Complete();

            lock (_testOutputLock)
            {
                _disposed = true;
            }

            if (_process != null)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                try
                {
                    _process.CancelErrorRead();
                }
                catch
                {
                }

                try
                {
                    _process.CancelOutputRead();
                }
                catch
                {
                }

                _process.ErrorDataReceived -= OnData;
                _process.OutputDataReceived -= OnData;
                _process.Exited -= OnExit;
                _process.Dispose();
                _process = null;
            }
        }
    }
}

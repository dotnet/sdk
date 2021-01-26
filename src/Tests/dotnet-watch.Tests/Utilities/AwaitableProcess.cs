// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Internal;
using Microsoft.NET.TestFramework.Commands;
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
        private bool _started;
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

        public void Start()
        {
            if (_process != null)
            {
                throw new InvalidOperationException("Already started");
            }

            var processStartInfo = _spec.GetProcessStartInfo();
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "true";

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
            _started = true;
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();
            WriteTestOutput($"{DateTime.Now}: process started: '{_process.StartInfo.FileName} {_process.StartInfo.Arguments}'");
        }

        public async Task<string> GetOutputLineAsync(string message, TimeSpan timeout)
        {
            WriteTestOutput($"Waiting for output line [msg == '{message}']. Will wait for {timeout.TotalSeconds} sec.");
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            return await GetOutputLineAsync($"[msg == '{message}']", m => string.Equals(m, message, StringComparison.Ordinal), cts.Token);
        }

        public async Task<string> GetOutputLineStartsWithAsync(string message, TimeSpan timeout)
        {
            WriteTestOutput($"Waiting for output line [msg.StartsWith('{message}')]. Will wait for {timeout.TotalSeconds} sec.");
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            return await GetOutputLineAsync($"[msg.StartsWith('{message}')]", m => m != null && m.StartsWith(message, StringComparison.Ordinal), cts.Token);
        }

        private async Task<string> GetOutputLineAsync(string predicateName, Predicate<string> predicate, CancellationToken cancellationToken)
        {
            while (!_source.Completion.IsCompleted)
            {
                while (await _source.OutputAvailableAsync(cancellationToken))
                {
                    var next = await _source.ReceiveAsync(cancellationToken);
                    _lines.Add(next);
                    var match = predicate(next);
                    WriteTestOutput($"{DateTime.Now}: recv: '{next}'. {(match ? "Matches" : "Does not match")} condition '{predicateName}'.");
                    if (match)
                    {
                        return next;
                    }
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
                    var next = await _source.ReceiveAsync(cancellationToken);
                    WriteTestOutput($"{DateTime.Now}: recv: '{next}'");
                    lines.Add(next);
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
            WriteTestOutput($"Process {_process.Id} has exited");
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
                if (_started && !_process.HasExited)
                {
                    _process.KillTree();
                }

                _process.CancelErrorRead();
                _process.CancelOutputRead();

                _process.ErrorDataReceived -= OnData;
                _process.OutputDataReceived -= OnData;
                _process.Exited -= OnExit;
                _process.Dispose();
            }
        }
    }
}

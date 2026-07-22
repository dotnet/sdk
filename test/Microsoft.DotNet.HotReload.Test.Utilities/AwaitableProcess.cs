// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal sealed class AwaitableProcess : IAsyncDisposable
    {
        // Per-wait timeout for expected process output. A single wait should never take anywhere near the
        // CI work-item timeout, so this is capped well below Microsoft.Testing.Platform's hang-dump timeout
        // (which fires at ~80% of the work-item timeout, e.g. 48 min for the 60-min work items configured by
        // TestWorkItemTimeout in test/UnitTests.proj). Without the cap, deriving the value directly from
        // HELIX_WORK_ITEM_TIMEOUT (60 min - 10 s = 59:50) means the hang-dump always fires first, converting a
        // single wedged test into a work-item-wide hang that kills the whole run and loses all results. With
        // the cap, a genuinely wedged wait fails *this* test fast with a useful "Output not found" message.
        // See https://github.com/dotnet/sdk/issues/55258 and https://github.com/dotnet/sdk/issues/55044.
        private static readonly TimeSpan s_timeout = GetOutputWaitTimeout();

        private static TimeSpan GetOutputWaitTimeout()
        {
            var cap = TimeSpan.FromMinutes(10);

            // Honor a shorter work-item timeout (fire just before it), but never exceed the cap.
            if (Environment.GetEnvironmentVariable("HELIX_WORK_ITEM_TIMEOUT") is { } value &&
                TimeSpan.TryParse(value, out var workItemTimeout))
            {
                var derived = workItemTimeout.Subtract(TimeSpan.FromSeconds(10));
                return derived < cap ? derived : cap;
            }

            return cap;
        }

        private readonly List<string> _lines = [];

        private CancellationTokenSource? _disposalCompletionSource = new();
        private readonly CancellationTokenSource _outputCompletionSource = new();

        private readonly Channel<string> _outputChannel = Channel.CreateUnbounded<string>(new()
        {
            SingleReader = true,
            SingleWriter = false
        });

        public int Id { get; }
        public Process Process { get; }
        public DebugTestOutputLogger Logger { get; }

        private readonly Task _processExitAwaiter;

        public AwaitableProcess(ProcessStartInfo processStartInfo, DebugTestOutputLogger logger)
        {
            Logger = logger;

            if (!processStartInfo.UseShellExecute && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var flags = File.GetUnixFileMode(processStartInfo.FileName);
                if (!flags.HasFlag(UnixFileMode.UserExecute))
                {
                    File.SetUnixFileMode(processStartInfo.FileName, flags | UnixFileMode.UserExecute);
                }
            }

            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.StandardOutputEncoding = Encoding.UTF8;
            processStartInfo.StandardErrorEncoding = Encoding.UTF8;

            Process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo,
            };

            Process.OutputDataReceived += OnData;
            Process.ErrorDataReceived += OnData;

            Process.Start();

            Process.BeginErrorReadLine();
            Process.BeginOutputReadLine();

            try
            {
                Id = Process.Id;
            }
            catch
            {
            }

            _processExitAwaiter = WaitForProcessExitAsync();
        }

        public IEnumerable<string> Output
            => _lines;

        public void ClearOutput()
            => _lines.Clear();

        public async Task WaitForProcessExitAsync()
        {
            while (true)
            {
                try
                {
                    if (Process.HasExited)
                    {
                        break;
                    }
                }
                catch
                {
                    break;
                }

                using var iterationCancellationSource = new CancellationTokenSource();
                iterationCancellationSource.CancelAfter(TimeSpan.FromSeconds(1));

                try
                {
                    await Process.WaitForExitAsync(iterationCancellationSource.Token);
                    break;
                }
                catch 
                {
                }
            }

            Logger.Log($"Process {Id} exited");
            _outputCompletionSource.Cancel();
        }

        public async Task<string> GetRequiredOutputLineAsync(Predicate<string> selector)
        {
            var line = await GetOutputLineAsync(selector);

            // process terminated without producing required output
            Assert.IsNotNull(line);

            return line;
        }

        public async Task<string?> GetOutputLineAsync(Predicate<string> selector)
        {
            var disposalCompletionSource = _disposalCompletionSource;
            ObjectDisposedException.ThrowIf(disposalCompletionSource == null, this);

            using var timeoutCancellation = new CancellationTokenSource();
            if (!Debugger.IsAttached)
            {
                timeoutCancellation.CancelAfter(s_timeout);
            }

            using var outputReadCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _outputCompletionSource.Token,
                disposalCompletionSource.Token,
                timeoutCancellation.Token);

            try
            {
                while (!outputReadCancellation.IsCancellationRequested)
                {
                    var line = await _outputChannel.Reader.ReadAsync(outputReadCancellation.Token);
                    _lines.Add(line);
                    if (selector(line))
                    {
                        return line;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutCancellation.Token.IsCancellationRequested)
                {
                    Assert.Fail($"Output not found within {s_timeout}");
                }

                if (disposalCompletionSource.Token.IsCancellationRequested)
                {
                    Assert.Fail($"Test disposed while waiting for output");
                }
            }

            // Read the remaining output that may have been written to
            // the channel but not read yet when the process exited.
            while (_outputChannel.Reader.TryRead(out var line))
            {
                _lines.Add(line);
                if (selector(line))
                {
                    return line;
                }
            }

            return null;
        }

        public Task WaitUntilOutputCompleted()
            => GetOutputLineAsync(_ => false);

        private void OnData(object sender, DataReceivedEventArgs args)
        {
            var line = args.Data ?? string.Empty;
            if (line.StartsWith("\x1b]"))
            {
                // strip terminal logger progress indicators from line
                line = line.StripTerminalLoggerProgressIndicators();
            }

            Logger.WriteLine(line);

            Assert.IsTrue(_outputChannel.Writer.TryWrite(line));
        }

        public async ValueTask DisposeAsync()
        {
            var disposalCompletionSource = Interlocked.Exchange(ref _disposalCompletionSource, null);
            ObjectDisposedException.ThrowIf(disposalCompletionSource == null, this);
            disposalCompletionSource.Cancel();

            Process.ErrorDataReceived -= OnData;
            Process.OutputDataReceived -= OnData;

            // Close stdin before killing the process to unblock any pending stdin reads
            // (e.g. PhysicalConsole.ListenToStandardInputAsync on Linux where stdin reads
            // don't unblock on process kill).
            try
            {
                Process.StandardInput?.Close();
            }
            catch
            {
            }

            try
            {
                Process.CancelErrorRead();
            }
            catch
            {
            }

            try
            {
                Process.CancelOutputRead();
            }
            catch
            {
            }

            try
            {
                Process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            // ensure process has exited
            await _processExitAwaiter;

            Process.Dispose();

            _outputCompletionSource.Dispose();
        }
    }
}

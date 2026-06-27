// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;

namespace Microsoft.DotNet.Watch.UnitTests
{
    internal sealed class AwaitableProcess : IAsyncDisposable
    {
        // Per-operation timeout for waiting on individual output lines.
        // Capped at 10 minutes to prevent a single hanging wait from consuming the entire
        // Helix work item timeout (typically 1 hour). This ensures other tests in the shard
        // still run even if one test's process never produces expected output.
        // The 90-second buffer before the work item timeout allows test cleanup (process kill,
        // tree teardown) to complete before Helix hard-kills the work item.
        private static readonly TimeSpan s_timeout = Environment.GetEnvironmentVariable("HELIX_WORK_ITEM_TIMEOUT") is { } value
            ? Min(TimeSpan.Parse(value).Subtract(TimeSpan.FromSeconds(90)), TimeSpan.FromMinutes(10))
            : TimeSpan.FromMinutes(10);

        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

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
            Assert.NotNull(line);

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
                    Assert.Fail($"Output not found within {s_timeout}. Process {Id} output so far ({_lines.Count} lines):{Environment.NewLine}{string.Join(Environment.NewLine, _lines.TakeLast(20))}");
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

            Assert.True(_outputChannel.Writer.TryWrite(line));
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

            // ensure process has exited (with a timeout to prevent cleanup hangs)
            var exitTask = _processExitAwaiter;
            if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(30))) != exitTask)
            {
                Logger.Log($"Process {Id} did not exit within 30s after kill");
            }

            Process.Dispose();

            _outputCompletionSource.Dispose();
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal class TestApplicationActionQueue
{
    private readonly Channel<ParallelizableTestModuleGroupWithSequentialInnerModules> _channel;
    private readonly Task[] _readers;

    private int? _aggregateExitCode;

    private readonly Lock _lock = new();

    public TestApplicationActionQueue(int degreeOfParallelism, BuildOptions buildOptions, TestOptions testOptions, TerminalTestReporter output, Action<CommandLineOptionMessages> onHelpRequested, CtrlCCancellationManager ctrlC)
    {
        _channel = Channel.CreateUnbounded<ParallelizableTestModuleGroupWithSequentialInnerModules>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        _readers = new Task[degreeOfParallelism];

        for (int i = 0; i < degreeOfParallelism; i++)
        {
            _readers[i] = Task.Run(async () => await Read(buildOptions, testOptions, output, onHelpRequested, ctrlC));
        }
    }

    public void Enqueue(ParallelizableTestModuleGroupWithSequentialInnerModules testApplication)
    {
        if (!_channel.Writer.TryWrite(testApplication))
        {
            throw new InvalidOperationException($"Failed to write to channel for test application: {testApplication}");
        }
    }

    public int CompleteEnqueueAndWait()
    {
        // Notify readers that no more data will be written
        _channel.Writer.Complete();

        Task.WaitAll(_readers);

        // If _aggregateExitCode is null, that means we didn't get any results.
        // So, we exit with "zero tests".
        return _aggregateExitCode ?? ExitCode.ZeroTests;
    }

    private async Task Read(BuildOptions buildOptions, TestOptions testOptions, TerminalTestReporter output, Action<CommandLineOptionMessages> onHelpRequested, CtrlCCancellationManager ctrlC)
    {
        try
        {
            await foreach (var nonParallelizedGroup in _channel.Reader.ReadAllAsync(ctrlC.Token))
            {
                foreach (var module in nonParallelizedGroup)
                {
                    ctrlC.Token.ThrowIfCancellationRequested();

                    int result = ExitCode.GenericFailure;
                    var testApp = new TestApplication(module, buildOptions, testOptions, output, onHelpRequested);
                    try
                    {
                        using (testApp)
                        {
                            result = await testApp.RunAsync(ctrlC);
                        }
                    }
                    catch (Exception ex)
                    {
                        var exAsString = ex.ToString();
                        Logger.LogTrace($"Exception running test module {module.RunProperties?.Command} {module.RunProperties?.Arguments}: {exAsString}");
                        Reporter.Error.WriteLine(string.Format(CliCommandStrings.ErrorRunningTestModule, module.RunProperties?.Command, module.RunProperties?.Arguments, exAsString));
                        result = ExitCode.GenericFailure;
                    }

                    // A module that ran zero tests (exit code 8) is not, by itself, a whole-run failure.
                    // With --test-modules or a global --filter, some modules may legitimately match no tests.
                    // Normalize it to success here; the aggregate "zero tests ran" verdict is decided once at
                    // the whole-run level in MicrosoftTestingPlatformTestCommand from the total test count. A
                    // stricter per-module minimum requested via -- --minimum-expected-tests N still fails that
                    // module with ExitCode.MinimumExpectedTestsPolicyViolation (9) and is preserved.
                    // See https://github.com/microsoft/testfx/issues/7457.
                    result = NormalizeExitCode(result, testApp.HasFailureDuringDispose);

                    lock (_lock)
                    {
                        if (_aggregateExitCode is null)
                        {
                            // This is the first result we are getting.
                            // So we assign the exit code, regardless of whether it's failure or success.
                            _aggregateExitCode = result;
                        }
                        else if (_aggregateExitCode.Value != result)
                        {
                            if (_aggregateExitCode == ExitCode.Success)
                            {
                                // The current result we are dealing with is the first failure after previous Success.
                                // So we assign the current failure.
                                _aggregateExitCode = result;
                            }
                            else if (result != ExitCode.Success)
                            {
                                // If we get a new failure result, which is different from a previous failure, we use GenericFailure.
                                _aggregateExitCode = ExitCode.GenericFailure;
                            }
                            else
                            {
                                // The current result is a success, but we already have a failure.
                                // So, we keep the failure exit code.
                            }
                        }
                    }
                }
            }

        }
        catch (OperationCanceledException) when (ctrlC.Token.IsCancellationRequested)
        {
            // Stop scheduling new test apps once the user has pressed Ctrl+C the first time.
            // Already-running test apps are left alone so they can gracefully cancel themselves
            // (and report final session state via IPC); a second Ctrl+C is what force-kills them
            // via the CtrlCCancellationManager.
        }
    }

    internal static int NormalizeExitCode(int result, bool hasFailureDuringDispose)
    {
        if (result == ExitCode.ZeroTests)
        {
            result = ExitCode.Success;
        }

        return result == ExitCode.Success && hasFailureDuringDispose
            ? ExitCode.GenericFailure
            : result;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Threading.Channels;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal class TestApplicationActionQueue
{
    private readonly Channel<ParallelizableTestModuleGroupWithSequentialInnerModules> _channel;
    private readonly List<Task> _readers;

    private int? _aggregateExitCode;

    private static readonly Lock _lock = new();

    public TestApplicationActionQueue(int degreeOfParallelism, BuildOptions buildOptions, TestOptions testOptions, TerminalTestReporter output, Func<TestApplication, Task<int>> action)
    {
        _channel = Channel.CreateUnbounded<ParallelizableTestModuleGroupWithSequentialInnerModules>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        _readers = [];

        for (int i = 0; i < degreeOfParallelism; i++)
        {
            _readers.Add(Task.Run(async () => await Read(action, buildOptions, testOptions, output)));
        }
    }

    public void Enqueue(ParallelizableTestModuleGroupWithSequentialInnerModules testApplication)
    {
        if (!_channel.Writer.TryWrite(testApplication))
        {
            throw new InvalidOperationException($"Failed to write to channel for test application: {testApplication}");
        }
    }

    public int WaitAllActions()
    {
        Task.WaitAll([.. _readers]);

        // If _aggregateExitCode is null, that means we didn't get any results.
        // So, we exit with "zero tests".
        return _aggregateExitCode ?? ExitCode.ZeroTests;
    }

    public void EnqueueCompleted()
    {
        //Notify readers that no more data will be written
        _channel.Writer.Complete();
    }

    private async Task Read(Func<TestApplication, Task<int>> action, BuildOptions buildOptions, TestOptions testOptions, TerminalTestReporter output)
    {
        await foreach (var nonParallelizedGroup in _channel.Reader.ReadAllAsync())
        {
            foreach (var module in nonParallelizedGroup)
            {
                int result = ExitCode.GenericFailure;
                var testApp = new TestApplication(module, buildOptions, testOptions, output);
                using (testApp)
                {
                    result = await action(testApp);
                }

                if (result == ExitCode.Success && testApp.HasFailureDuringDispose)
                {
                    result = ExitCode.GenericFailure;
                }
                
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
}

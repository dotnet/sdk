// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;

namespace Microsoft.DotNet.Cli;

internal class TestApplicationActionQueue
{
    private readonly Channel<TestApplication> _channel;
    private readonly List<Task> _readers;

    private int? _exitCode;

    private static readonly Lock _lock = new();

    public TestApplicationActionQueue(int degreeOfParallelism, Func<TestApplication, Task<int>> action)
    {
        _channel = Channel.CreateUnbounded<TestApplication>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        _readers = [];

        for (int i = 0; i < degreeOfParallelism; i++)
        {
            _readers.Add(Task.Run(async () => await Read(action)));
        }
    }

    public void Enqueue(TestApplication testApplication)
    {
        if (!_channel.Writer.TryWrite(testApplication))
        {
            throw new InvalidOperationException($"Failed to write to channel for test application: {testApplication}");
        }
    }

    public int WaitAllActions()
    {
        Task.WaitAll([.. _readers]);

        // If _exitCode is null, that means we didn't get any results.
        // So, we exit with "zero tests".
        return _exitCode ?? ExitCode.ZeroTests;
    }

    public void EnqueueCompleted()
    {
        //Notify readers that no more data will be written
        _channel.Writer.Complete();
    }

    private async Task Read(Func<TestApplication, Task<int>> action)
    {
        await foreach (var testApp in _channel.Reader.ReadAllAsync())
        {
            int result = await action(testApp);

            lock (_lock)
            {
                if (_exitCode is null)
                {
                    // This is the first result we are getting.
                    // So we assign the exit code.
                    _exitCode = result;
                }
                else if (_exitCode.Value != result)
                {
                    // If our aggregate exitCode is different from the current result, we use the generic failure code.
                    _exitCode = ExitCode.GenericFailure;
                }
            }
        }
    }
}

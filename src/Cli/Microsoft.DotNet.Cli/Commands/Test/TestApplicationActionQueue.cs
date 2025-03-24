// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;

namespace Microsoft.DotNet.Cli;

internal class TestApplicationActionQueue
{
    private readonly Channel<TestApplication> _channel;
    private readonly List<Task> _readers;

    private bool _hasFailed;
    private int? _firstExitCode;
    private bool _allSameExitCode;

    private static readonly Lock _lock = new();

    public TestApplicationActionQueue(int degreeOfParallelism, Func<TestApplication, Task<int>> action)
    {
        _channel = Channel.CreateUnbounded<TestApplication>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        _readers = [];
        _hasFailed = false;
        _firstExitCode = null;
        _allSameExitCode = true;

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

        if (_allSameExitCode && _firstExitCode.HasValue)
        {
            return _firstExitCode.Value;
        }

        return _hasFailed ? ExitCode.GenericFailure : ExitCode.Success;
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
                if (_firstExitCode == null)
                {
                    _firstExitCode = result;
                }
                else if (_firstExitCode != result)
                {
                    _allSameExitCode = false;
                }

                if (result != ExitCode.Success)
                {
                    _hasFailed = true;
                }
            }
        }
    }
}

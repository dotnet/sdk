// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;

namespace Microsoft.DotNet.Cli
{
    internal class TestApplicationActionQueue
    {
        private readonly Channel<TestApplication> _channel = Channel.CreateUnbounded<TestApplication>(new UnboundedChannelOptions() { SingleReader = false, SingleWriter = false });
        private readonly List<Task> _readers = [];
        private bool _hasFailed = false;

        public TestApplicationActionQueue(int dop, Func<TestApplication, Task<int>> action)
        {
            // Add readers to the channel, to read the test applications
            for (int i = 0; i < dop; i++)
            {
                _readers.Add(Task.Run(async () => await Read(action)));
            }
        }

        public void Enqueue(TestApplication testApplication)
        {
            if (!_channel.Writer.TryWrite(testApplication))
                throw new InvalidOperationException($"Failed to write to channel for test application: {testApplication}");
        }

        public bool WaitAllActions()
        {
            Task.WaitAll([.. _readers]);
            return _hasFailed;
        }

        public void EnqueueCompleted()
        {
            //Notify readers that no more data will be written
            _channel.Writer.Complete();
        }

        private async Task Read(Func<TestApplication, Task<int>> action)
        {
            while (await _channel.Reader.WaitToReadAsync())
            {
                if (_channel.Reader.TryRead(out TestApplication testApp))
                {
                    int result = await action(testApp);

                    if (result != ExitCodes.Success)
                    {
                        _hasFailed = true;
                    }
                }
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal class TestApplicationActionQueue
    {
        private readonly Channel<TestApplication> _channel = Channel.CreateUnbounded<TestApplication>(new UnboundedChannelOptions() { SingleReader = false, SingleWriter = false });
        private readonly List<Task> _readers = [];

        public TestApplicationActionQueue(int dop, Func<TestApplication, Task> action)
        {
            // Add readers to the channel, to read the test applications
            for (int i = 0; i < dop; i++)
            {
                _readers.Add(Task.Run(async () => await Read(action)));
            }
        }

        public async Task Enqueue(TestApplication testApplication)
        {
            try
            {
                await _channel.Writer.WriteAsync(testApplication);
            }
            catch (Exception ex)
            {
                VSTestTrace.SafeWriteTrace(() => $"Failed to write to channel for test application: {testApplication.ModulePath}:\n {ex}");
                throw;
            }
        }

        public void WaitAllActions()
        {
            Task.WaitAll([.. _readers]);
        }

        public void EnqueueCompleted()
        {
            //Notify readers that no more data will be written
            _channel.Writer.Complete();
        }

        private async Task Read(Func<TestApplication, Task> action)
        {
            while (await _channel.Reader.WaitToReadAsync())
            {
                if (_channel.Reader.TryRead(out TestApplication testApp))
                {
                    await action(testApp);
                }
            }
        }
    }
}

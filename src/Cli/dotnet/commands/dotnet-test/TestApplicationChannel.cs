// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;

namespace Microsoft.DotNet.Cli.commands.dotnet_test
{
    internal class TestApplicationChannel
    {
        private readonly Channel<TestApplication> _channel = Channel.CreateUnbounded<TestApplication>(new UnboundedChannelOptions() { SingleReader = false, SingleWriter = false });
        private readonly List<Task> _readers = [];
        private readonly List<Task> _writers = [];

        public void ReadFromChannel()
        {
            _readers.Add(Task.Run(async () => await Read()));
        }

        public void WriteToChannel(TestApplication testApplication)
        {
            _writers.Add(Task.Run(() => _channel.Writer.TryWrite(testApplication)));
        }

        public async Task WaitForWriters()
        {
            await Task.WhenAll(_writers);
        }

        public void WaitForReaders()
        {
            Task.WaitAll([.. _readers]);
        }

        public void NotifyWritingIsComplete()
        {
            //Notify consumers that no more data will be written
            _channel.Writer.Complete();
        }

        private async Task Read()
        {
            while (await _channel.Reader.WaitToReadAsync())
            {
                if (_channel.Reader.TryRead(out TestApplication testApplication))
                {
                    await testApplication.RunAsync();
                }
            }
        }
    }
}

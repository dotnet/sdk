// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class ChunkStreamReadTests : TestBase, IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public ChunkStreamReadTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact]
        public void VerifyLongStreamNoReplacement()
        {
            Random rnd = new Random();
            byte[] valueBytes = new byte[1024 * 1024];
            rnd.NextBytes(valueBytes);
            ChunkMemoryStream input = new ChunkMemoryStream(valueBytes, 512);
            ChunkMemoryStream output = new ChunkMemoryStream(1024);

            IOperationProvider[] operations = Array.Empty<IOperationProvider>();
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            processor.Run(input, output, 1024);
            Assert.Equal(input.Length, output.Length);

            int file1byte;
            int file2byte;
            do
            {
                file1byte = input.ReadByte();
                file2byte = output.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));
            Assert.Equal(0, file1byte - file2byte);
        }

        [Fact]
        public void VerifyLongStreamWithReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            StringBuilder valueBuilder = new StringBuilder();
            StringBuilder expectedBuilder = new StringBuilder();

            for (int i = 0; i < 1024; i++)
            {
                valueBuilder.Append(value);
                expectedBuilder.Append(expected);
            }
            value = valueBuilder.ToString();
            expected = expectedBuilder.ToString();

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            ChunkMemoryStream input = new ChunkMemoryStream(valueBytes, 10);
            ChunkMemoryStream output = new ChunkMemoryStream(10);

            IOperationProvider[] operations = { new Replacement("value".TokenConfig(), "foo", null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1000);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyLongStreamWithReplacementBeforeAfter()
        {
            string value = @"test valueA before valueA after valueB valueB";
            string expected = @"test foo before valueA after bar valueB";

            StringBuilder valueBuilder = new StringBuilder();
            StringBuilder expectedBuilder = new StringBuilder();

            for (int i = 0; i < 1024; i++)
            {
                valueBuilder.Append(value);
                expectedBuilder.Append(expected);
            }
            value = valueBuilder.ToString();
            expected = expectedBuilder.ToString();

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            ChunkMemoryStream input = new ChunkMemoryStream(valueBytes, 10);
            ChunkMemoryStream output = new ChunkMemoryStream(10);

            IOperationProvider[] operations =
                {
                    new Replacement("valueA".TokenConfigBuilder().OnlyIfBefore(" before"), "foo", null, true),
                    new Replacement("valueB".TokenConfigBuilder().OnlyIfAfter("after "), "bar", null, true),
                };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, VariableCollection.Environment(_engineEnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1000);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyConsumeWholeLine()
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.ConsumeWholeLine(ref length, ref position);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There    \r\n    You");
            Stream input = new ChunkMemoryStream(data, 1);
            Stream output = new ChunkMemoryStream(1);
            bool changed = processor.Run(input, output, 5);

            Verify(Encoding.UTF8, output, changed, "Hello    \r\n    There    \r\n    You", "Hello    \r\n    You");
        }

        private class ChunkMemoryStream : MemoryStream
        {
            private readonly int _chunkSize;

            internal ChunkMemoryStream(int chunkSize) : base()
            {
                _chunkSize = chunkSize;
            }

            internal ChunkMemoryStream(byte[] buffer, int chunkSize) : base(buffer)
            {
                _chunkSize = chunkSize;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (count > _chunkSize)
                {
                    count = _chunkSize;
                }
                return base.Read(buffer, offset, count);
            }
        }
    }
}

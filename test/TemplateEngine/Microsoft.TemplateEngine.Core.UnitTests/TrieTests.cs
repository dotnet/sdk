// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class TrieTests : TestBase, IClassFixture<TestLoggerFactory>
    {
        private readonly ILogger _logger;

        public TrieTests(TestLoggerFactory testLoggerFactory)
        {
            _logger = testLoggerFactory.CreateLogger();
        }

        [Fact]
        public void VerifyThatTrieMatchesAtTheBeginning()
        {
            bool testActivated = false;
            IProcessor p = SetupTestProcessor(
                new IOperationProvider[]
                {
                    new MockOperationProvider(
                        new MockOperation(
                            null,
                            (processor, bufferLength, ref currentBufferPosition, token) =>
                            {
                                testActivated = true;
                                return 0;
                            },
                            true,
                            new byte[] { 1, 2, 3 }))
                },
                VariableCollection.Root());

            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            MemoryStream source = new MemoryStream(data)
            {
                Position = 0
            };
            p.Run(source, new MemoryStream());
            Assert.True(testActivated);
        }

        [Fact]
        public void VerifyThatTrieMatchesAtTheEnd()
        {
            bool testActivated = false;
            IProcessor p = SetupTestProcessor(
                new IOperationProvider[]
                {
                    new MockOperationProvider(
                        new MockOperation(
                            null,
                            (processor, bufferLength, ref currentBufferPosition, token) =>
                            {
                                testActivated = true;
                                return 0;
                            },
                            true,
                            new byte[] { 1, 2, 3 }))
                },
                VariableCollection.Root());

            byte[] data = new byte[] { 4, 5, 1, 2, 3 };
            p.Run(new MemoryStream(data), new MemoryStream());
            Assert.True(testActivated);
        }

        [Fact]
        public void VerifyThatTrieMatchesInTheInterior()
        {
            bool testActivated = false;
            IProcessor p = SetupTestProcessor(
                new IOperationProvider[]
                {
                    new MockOperationProvider(
                        new MockOperation(
                            null,
                            (processor, bufferLength, ref currentBufferPosition, token) =>
                            {
                                testActivated = true;
                                return 0;
                            },
                            true,
                            new byte[] { 1, 2, 3 }))
                },
                VariableCollection.Root());

            byte[] data = new byte[] { 4, 5, 1, 2, 3, 6, 7 };
            p.Run(new MemoryStream(data), new MemoryStream());
            Assert.True(testActivated);
        }

        [Fact]
        public void VerifyThatTrieMatchesAsTheWholeContents()
        {
            bool testActivated = false;
            IProcessor p = SetupTestProcessor(
                new IOperationProvider[]
                {
                    new MockOperationProvider(
                        new MockOperation(
                            null,
                            (processor, bufferLength, ref currentBufferPosition, token) =>
                            {
                                testActivated = true;
                                return 0;
                            },
                            true,
                            new byte[] { 1, 2, 3 }))
                },
                VariableCollection.Root());

            byte[] data = new byte[] { 1, 2, 3 };
            p.Run(new MemoryStream(data), new MemoryStream());
            Assert.True(testActivated);
        }

        private IProcessor SetupTestProcessor(IOperationProvider[] operations, VariableCollection vc)
        {
            EngineConfig cfg = new EngineConfig(_logger, vc);
            return Processor.Create(cfg, operations);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class TrieTests : TestBase, IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public TrieTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
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
                            (IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target) =>
                            {
                                testActivated = true;
                                return 0;
                            },
                            true,
                            new byte[] { 1, 2, 3 })
                    )
                },
                VariableCollection.Root());

            byte[] data = new byte[] { 1, 2, 3, 4, 5 };
            MemoryStream source = new MemoryStream(data);
            source.Position = 0;
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
                            (IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target) =>
                            {
                                testActivated = true;
                                return 0;
                            },
                            true,
                            new byte[] { 1, 2, 3 })
                    )
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
                            (IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target) =>
                            {
                                testActivated = true;
                                return 0;
                            },
                            true,
                            new byte[] { 1, 2, 3 })
                    )
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
                            (IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target) =>
                            {
                                testActivated = true;
                                return 0;
                            },
                            true,
                            new byte[] { 1, 2, 3 })
                    )
                },
                VariableCollection.Root());

            byte[] data = new byte[] { 1, 2, 3 };
            p.Run(new MemoryStream(data), new MemoryStream());
            Assert.True(testActivated);
        }

        private IProcessor SetupTestProcessor(IOperationProvider[] operations, VariableCollection vc)
        {
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, vc);
            return Processor.Create(cfg, operations);
        }
    }
}

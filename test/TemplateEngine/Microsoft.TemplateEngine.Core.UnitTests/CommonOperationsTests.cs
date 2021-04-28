// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class CommonOperationsTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;

        public CommonOperationsTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [Fact(DisplayName = nameof(VerifyTrimWhitespaceForward))]
        public void VerifyTrimWhitespaceForward()
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.TrimWhitespace(true, false, ref length, ref position);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("Hello"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("    There", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyTrimWhitespaceBackward))]
        public void VerifyTrimWhitespaceBackward()
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.TrimWhitespace(false, true, ref length, ref position);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyTrimWhitespaceBothDirections))]
        public void VerifyTrimWhitespaceBothDirections()
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.TrimWhitespace(true, true, ref length, ref position);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There    \r\n    You");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n    You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyTrimWhitespaceNeitherDirection))]
        public void VerifyTrimWhitespaceNeitherDirection()
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.TrimWhitespace(false, false, ref length, ref position);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There    \r\n    You");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n        \r\n    You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyConsumeWholeLine))]
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
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n    You", outcomeString);
        }

        [Theory(DisplayName = nameof(VerifyWhitespaceHandlerConsumeWholeLine))]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void VerifyWhitespaceHandlerConsumeWholeLine(bool trim, bool trimForward, bool trimBackward)
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.WhitespaceHandler(ref length, ref position, true, trim, trimForward, trimBackward);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There    \r\n    You");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n    You", outcomeString);
        }

        [Theory(DisplayName = nameof(VerifyWhitespaceHandlerTrim))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void VerifyWhitespaceHandlerTrim(bool trimForward, bool trimBackward)
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.WhitespaceHandler(ref length, ref position, false, true, trimForward, trimBackward);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There    \r\n    You");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n    You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyWhitespaceHandlerTrimForwardButNotBack))]
        public void VerifyWhitespaceHandlerTrimForwardButNotBack()
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.WhitespaceHandler(ref length, ref position, false, false, true, false);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There     \r\n    You");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n        You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyWhitespaceHandlerTrimBackButNotForward))]
        public void VerifyWhitespaceHandlerTrimBackButNotForward()
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.WhitespaceHandler(ref length, ref position, false, false, false, true);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There     \r\n    You");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n     \r\n    You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyWhitespaceHandlerTrimBackAndForward))]
        public void VerifyWhitespaceHandlerTrimBackAndForward()
        {
            MockOperation o = new MockOperation(
                null,
                (IProcessorState state, int length, ref int position, int token, Stream target) =>
                {
                    state.WhitespaceHandler(ref length, ref position, false, false, true, true);
                    return 0;
                },
                true,
                Encoding.UTF8.GetBytes("There"));

            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = Encoding.UTF8.GetBytes("Hello    \r\n    There     \r\n    You");
            Stream d = new MemoryStream(data);
            Stream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            byte[] outcome = new byte[result.Length];
            result.Read(outcome, 0, outcome.Length);
            string outcomeString = Encoding.UTF8.GetString(outcome);
            Assert.Equal("Hello    \r\n    You", outcomeString);
        }
    }
}

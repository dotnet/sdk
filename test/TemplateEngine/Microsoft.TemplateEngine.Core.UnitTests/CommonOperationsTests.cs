// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class CommonOperationsTests : IClassFixture<TestLoggerFactory>
    {
        private readonly ILogger _logger;

        public CommonOperationsTests(TestLoggerFactory testLoggerFactory)
        {
            _logger = testLoggerFactory.CreateLogger();
        }

        [Fact(DisplayName = nameof(VerifyTrimWhitespaceForward))]
        public void VerifyTrimWhitespaceForward()
        {
            MockOperation o = new MockOperation(
                null,
                (state, length, ref position, token) =>
                {
                    state.TrimWhitespace(true, false, ref length, ref position);
                    return 0;
                },
                true,
                "Hello"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.Equal("    There", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyTrimWhitespaceBackward))]
        public void VerifyTrimWhitespaceBackward()
        {
            MockOperation o = new MockOperation(
                null,
                (state, length, ref position, token) =>
                {
                    state.TrimWhitespace(false, true, ref length, ref position);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.Equal("Hello    \r\n", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyTrimWhitespaceBothDirections))]
        public void VerifyTrimWhitespaceBothDirections()
        {
            MockOperation o = new MockOperation(
                null,
                (state, length, ref position, token) =>
                {
                    state.TrimWhitespace(true, true, ref length, ref position);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There    \r\n    You"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.Equal("Hello    \r\n    You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyTrimWhitespaceNeitherDirection))]
        public void VerifyTrimWhitespaceNeitherDirection()
        {
            MockOperation o = new MockOperation(
                null,
                (state, length, ref position, token) =>
                {
                    state.TrimWhitespace(false, false, ref length, ref position);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There    \r\n    You"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.Equal("Hello    \r\n        \r\n    You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyConsumeWholeLine))]
        public void VerifyConsumeWholeLine()
        {
            MockOperation o = new MockOperation(
                null,
                (state, length, ref position, token) =>
                {
                    state.ConsumeWholeLine(ref length, ref position);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There    \r\n    You"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
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
                (state, length, ref position, token) =>
                {
                    state.WhitespaceHandler(ref length, ref position, true, trim, trimForward, trimBackward);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There    \r\n    You"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
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
                (state, length, ref position, token) =>
                {
                    state.WhitespaceHandler(ref length, ref position, false, true, trimForward, trimBackward);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There    \r\n    You"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            result.Position = 0;
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.Equal("Hello    \r\n    You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyWhitespaceHandlerTrimForwardButNotBack))]
        public void VerifyWhitespaceHandlerTrimForwardButNotBack()
        {
            MockOperation o = new MockOperation(
                null,
                (state, length, ref position, token) =>
                {
                    state.WhitespaceHandler(ref length, ref position, false, false, true, false);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There     \r\n    You"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.Equal("Hello    \r\n        You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyWhitespaceHandlerTrimBackButNotForward))]
        public void VerifyWhitespaceHandlerTrimBackButNotForward()
        {
            MockOperation o = new MockOperation(
                null,
                (state, length, ref position, token) =>
                {
                    state.WhitespaceHandler(ref length, ref position, false, false, false, true);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There     \r\n    You"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.Equal("Hello    \r\n     \r\n    You", outcomeString);
        }

        [Fact(DisplayName = nameof(VerifyWhitespaceHandlerTrimBackAndForward))]
        public void VerifyWhitespaceHandlerTrimBackAndForward()
        {
            MockOperation o = new MockOperation(
                null,
                (state, length, ref position, token) =>
                {
                    state.WhitespaceHandler(ref length, ref position, false, false, true, true);
                    return 0;
                },
                true,
                "There"u8.ToArray());

            EngineConfig cfg = new EngineConfig(_logger, new VariableCollection());
            IProcessor processor = Processor.Create(cfg, o.Provider);
            byte[] data = "Hello    \r\n    There     \r\n    You"u8.ToArray();
            Stream d = new MemoryStream(data);
            MemoryStream result = new MemoryStream();
            bool modified = processor.Run(d, result);

            Assert.True(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.Equal("Hello    \r\n    You", outcomeString);
        }
    }
}

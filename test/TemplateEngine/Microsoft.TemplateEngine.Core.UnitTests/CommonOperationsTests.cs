// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    [TestClass]
    public class CommonOperationsTests
    {
        private static TestLoggerFactory s_loggerFactory = null!;
        private readonly ILogger _logger;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_loggerFactory = new TestLoggerFactory();

        [ClassCleanup]
        public static void ClassCleanup() => s_loggerFactory?.Dispose();

        public CommonOperationsTests()
        {
            _logger = s_loggerFactory.CreateLogger();
        }

        [TestMethod]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("    There", outcomeString);
        }

        [TestMethod]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n", outcomeString);
        }

        [TestMethod]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n    You", outcomeString);
        }

        [TestMethod]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n        \r\n    You", outcomeString);
        }

        [TestMethod]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n    You", outcomeString);
        }

        [TestMethod]
        [CombinatorialData]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n    You", outcomeString);
        }

        [TestMethod]
        [CombinatorialData]
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

            Assert.IsTrue(modified);
            result.Position = 0;
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n    You", outcomeString);
        }

        [TestMethod]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n        You", outcomeString);
        }

        [TestMethod]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n     \r\n    You", outcomeString);
        }

        [TestMethod]
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

            Assert.IsTrue(modified);
            string outcomeString = Encoding.UTF8.GetString(result.ToArray());
            Assert.AreEqual("Hello    \r\n    You", outcomeString);
        }
    }
}

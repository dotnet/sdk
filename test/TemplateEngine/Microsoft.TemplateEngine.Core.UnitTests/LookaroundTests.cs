// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    [TestClass]
    public class LookaroundTests : TestBase
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        public LookaroundTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
        }

        [TestMethod]
        public void TestLookBehindMatches()
        {
            string value = @"aababcabcacc";
            string expected = @"aababca!cacc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("b".TokenConfigBuilder().OnlyIfAfter("ca"), "!", null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLookAheadMatches()
        {
            string value = @"aababcabcacc";
            string expected = @"aaba!cabcacc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("b".TokenConfigBuilder().OnlyIfBefore("cab"), "!", null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLookAroundMatches()
        {
            string value = @"aababcabcacc";
            string expected = @"aaba!cabcacc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("b".TokenConfigBuilder().OnlyIfBefore("cab").OnlyIfAfter("ba"), "!", null, true) };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLookaroundMatchLengthBehavior()
        {
            string value = @"background-color:white;
color:white;";
            string expected = @"background-color:blue;
color:red;";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement("white".TokenConfigBuilder().OnlyIfBefore(";").OnlyIfAfter("background-color:"), "blue", null, true),
                new Replacement("white".TokenConfigBuilder().OnlyIfBefore(";").OnlyIfAfter("color:"), "red", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestFullyOverlappedMatchBehavior()
        {
            string value = @"foobarbaz";
            string expected = @"abc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement("foo".TokenConfigBuilder().OnlyIfBefore("bar"), "a", null, true),
                new Replacement("bar".TokenConfigBuilder().OnlyIfBefore("baz").OnlyIfAfter("foo"), "b", null, true),
                new Replacement("baz".TokenConfigBuilder().OnlyIfAfter("bar"), "c", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLookbehindOverlappedMatchBehavior()
        {
            string value = @"foobarxaz";
            string expected = @"abc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement("foo".TokenConfigBuilder().OnlyIfBefore("bar"), "a", null, true),
                new Replacement("bar".TokenConfigBuilder().OnlyIfAfter("foo"), "b", null, true),
                new Replacement("xaz".TokenConfigBuilder().OnlyIfAfter("bar"), "c", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLookaheadOverlappedMatchBehavior()
        {
            string value = @"foobarbaz";
            string expected = @"abc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement("foo".TokenConfigBuilder().OnlyIfBefore("bar"), "a", null, true),
                new Replacement("bar".TokenConfigBuilder().OnlyIfBefore("baz"), "b", null, true),
                new Replacement("baz".TokenConfigBuilder().OnlyIfAfter("bar"), "c", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestReadAheadBreaksLookBehinds()
        {
            string value = @"footbarbaz";
            string expected = @"barbaz";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new MockOperationProvider(new MockOperation(null, ReadaheadOneByte, true, "foo"u8.ToArray())),
                new Replacement("bar".TokenConfigBuilder().OnlyIfAfter("foot"), "b", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLookBehindWithValueOverlappingPriorMatchGetsSkipped()
        {
            string value = @"foobarbaz";
            string expected = @"aarc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement("foob".TokenConfigBuilder(), "a", null, true),
                new Replacement("bar".TokenConfigBuilder().OnlyIfAfter("foo"), "b", null, true),
                new Replacement("baz".TokenConfigBuilder().OnlyIfAfter("bar"), "c", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLookBehindCoveringMatchedValueGetsMatched()
        {
            string value = @"foobarbaz";
            string expected = @"fooba";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement("baz".TokenConfigBuilder().OnlyIfAfter("foobar"), "a", null, true),
                new Replacement("bar".TokenConfigBuilder().OnlyIfAfter("foo"), "b", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLookAroundsCanBeUsedForInsertion()
        {
            string value = @"foobaz";
            string expected = @"foobarbaz";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement(string.Empty.TokenConfigBuilder().OnlyIfAfter("foo").OnlyIfBefore("baz"), "bar", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [TestMethod]
        public void TestLongestActualWins()
        {
            string value = @"foobarbaz";
            string expected = @"testarbaz";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement("foob".TokenConfigBuilder().OnlyIfBefore("arbaz"), "test", null, true),
                new Replacement("foo".TokenConfigBuilder().OnlyIfBefore("barbaz"), "test2", null, true)
            };
            EngineConfig cfg = new EngineConfig(_engineEnvironmentSettings.Host.Logger, VariableCollection.Root());
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        private static int ReadaheadOneByte(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token)
        {
            ++currentBufferPosition;
            return 0;
        }
    }
}

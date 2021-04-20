// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class LookaroundTests : TestBase
    {
        [Fact(DisplayName = nameof(TestLookBehindMatches))]
        public void TestLookBehindMatches()
        {
            string value = @"aababcabcacc";
            string expected = @"aababca!cacc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("b".TokenConfigBuilder().OnlyIfAfter("ca"), "!", null, true) };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLookAheadMatches))]
        public void TestLookAheadMatches()
        {
            string value = @"aababcabcacc";
            string expected = @"aaba!cabcacc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("b".TokenConfigBuilder().OnlyIfBefore("cab"), "!", null, true) };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLookAroundMatches))]
        public void TestLookAroundMatches()
        {
            string value = @"aababcabcacc";
            string expected = @"aaba!cabcacc";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacement("b".TokenConfigBuilder().OnlyIfBefore("cab").OnlyIfAfter("ba"), "!", null, true) };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLookaroundMatchLengthBehavior))]
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
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestFullyOverlappedMatchBehavior))]
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
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLookbehindOverlappedMatchBehavior))]
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
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLookaheadOverlappedMatchBehavior))]
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
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestReadAheadBreaksLookBehinds))]
        public void TestReadAheadBreaksLookBehinds()
        {
            string value = @"footbarbaz";
            string expected = @"barbaz";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new MockOperationProvider(new MockOperation(null, ReadaheadOneByte, true, Encoding.UTF8.GetBytes("foo"))),
                new Replacement("bar".TokenConfigBuilder().OnlyIfAfter("foot"), "b", null, true)
            };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLookBehindWithValueOverlappingPriorMatchGetsSkipped))]
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
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLookBehindCoveringMatchedValueGetsMatched))]
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
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLookAroundsCanBeUsedForInsertion))]
        public void TestLookAroundsCanBeUsedForInsertion()
        {
            string value = @"foobaz";
            string expected = @"foobarbaz";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations =
            {
                new Replacement("".TokenConfigBuilder().OnlyIfAfter("foo").OnlyIfBefore("baz"), "bar", null, true)
            };
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact(DisplayName = nameof(TestLongestActualWins))]
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
            EngineConfig cfg = new EngineConfig(EnvironmentSettings, VariableCollection.Environment(EnvironmentSettings), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        private static int ReadaheadOneByte(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
        {
            ++currentBufferPosition;
            return 0;
        }
    }
}

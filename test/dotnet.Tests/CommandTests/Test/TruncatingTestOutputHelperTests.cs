// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    [TestClass]
    public class TruncatingTestOutputHelperTests
    {
        private sealed class RecordingOutputHelper : ITestOutputHelper
        {
            private readonly StringBuilder _output = new();

            public List<string> Lines { get; } = new();

            public string Output => _output.ToString();

            public void Write(string message) => _output.Append(message);

            public void Write(string format, params object[] args) => Write(string.Format(format, args));

            public void WriteLine(string message)
            {
                Lines.Add(message);
                _output.AppendLine(message);
            }

            public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));
        }

        private static bool IsOmissionNote(string line) => line.Contains(nameof(TruncatingTestOutputHelper));

        [TestMethod]
        public void OutputWithinBudgetIsForwardedUnchanged()
        {
            var inner = new RecordingOutputHelper();
            using (var helper = new TruncatingTestOutputHelper(inner))
            {
                helper.WriteLine("first");
                helper.WriteLine("second");
            }

            inner.Lines.Should().Equal("first", "second");
        }

        [TestMethod]
        public void WriteDoesNotAppendNewLine()
        {
            var inner = new RecordingOutputHelper();
            using (var helper = new TruncatingTestOutputHelper(inner, maxHeadCharacters: 5, maxTailCharacters: 5))
            {
                helper.Write("head-");
                helper.Write("middle");
                helper.Write("-tail");

                helper.Output.Should().Be("head-");
            }

            inner.Output.Should().EndWith("-tail");
            inner.Output.Should().NotEndWith("-tail" + Environment.NewLine);
        }

        [TestMethod]
        public void MiddleIsDroppedButHeadAndTailArePreserved()
        {
            var inner = new RecordingOutputHelper();
            using (var helper = new TruncatingTestOutputHelper(inner, maxHeadCharacters: 5, maxTailCharacters: 5))
            {
                helper.WriteLine("aaaaa"); // fills the head
                helper.WriteLine("bbbbb"); // middle, should be dropped
                helper.WriteLine("ccccc"); // most recent, should be kept as tail
            }

            inner.Lines.First().Should().Be("aaaaa");
            inner.Lines.Last().Should().Be("ccccc");
            inner.Lines.Should().ContainSingle(l => IsOmissionNote(l));
            inner.Lines.Should().NotContain("bbbbb");
        }

        [TestMethod]
        public void TailKeepsRecentContentWhenFinalLineIsTiny()
        {
            // Michael Simons' review edge case: a short final line must not evict a much larger
            // immediately-preceding line and leave the tail with essentially nothing.
            var inner = new RecordingOutputHelper();
            using (var helper = new TruncatingTestOutputHelper(inner, maxHeadCharacters: 0, maxTailCharacters: 10))
            {
                helper.WriteLine(new string('A', 9));
                helper.WriteLine("BBB");
            }

            string tail = string.Concat(inner.Lines.Where(l => !IsOmissionNote(l)));
            // The retained tail holds the most recent ~10 characters (the trailing 'A's plus "BBB"),
            // not just the tiny final "BBB" (which is what whole-line eviction would have left).
            tail.Should().Be(new string('A', 7) + "BBB");
            tail.Length.Should().Be(10);
        }

        [TestMethod]
        public void SingleMessageLargerThanBudgetForwardsOnlyHeadAndTailPortions()
        {
            var inner = new RecordingOutputHelper();
            using (var helper = new TruncatingTestOutputHelper(inner, maxHeadCharacters: 5, maxTailCharacters: 5))
            {
                helper.WriteLine(new string('x', 100));
            }

            int forwardedCharacters = inner.Lines.Where(l => !IsOmissionNote(l)).Sum(l => l.Length);
            forwardedCharacters.Should().Be(10); // 5 head + 5 tail, not the whole 100
            inner.Lines.Should().ContainSingle(l => IsOmissionNote(l));
        }

        [TestMethod]
        public void WriteBufferedTailIsIdempotent()
        {
            var inner = new RecordingOutputHelper();
            var helper = new TruncatingTestOutputHelper(inner, maxHeadCharacters: 0, maxTailCharacters: 5);
            helper.WriteLine("abcdef"); // 6 chars, trimmed to last 5

            helper.WriteBufferedTail();
            int countAfterFirstFlush = inner.Lines.Count;
            helper.WriteBufferedTail();

            inner.Lines.Count.Should().Be(countAfterFirstFlush);
            inner.Lines.Last().Should().Be("bcdef");
        }
    }
}

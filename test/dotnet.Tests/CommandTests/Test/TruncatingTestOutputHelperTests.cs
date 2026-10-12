// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class TruncatingTestOutputHelperTests
    {
        private sealed class RecordingOutputHelper : ITestOutputHelper
        {
            public List<string> Lines { get; } = new();

            public void WriteLine(string message) => Lines.Add(message);

            public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
        }

        private static bool IsOmissionNote(string line) => line.Contains(nameof(TruncatingTestOutputHelper));

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;

namespace dotnet.Tests.CommandTests.Test;

/// <summary>
/// Regression coverage for https://github.com/dotnet/sdk/issues/55549. The collector both streams
/// the test application's output to the terminal as it is produced and keeps a bounded tail for the
/// failure summaries. It has to tell those two consumers apart, otherwise everything it streamed is
/// printed a second time by the summary.
/// </summary>
[TestClass]
public class ProcessOutputCollectorTests
{
    private const int TailLineCount = 3;

    /// <summary>
    /// Before the protocol version is known the streaming state is <see langword="null"/>: nothing may
    /// reach the terminal yet, and the capture has to stay available so a failure summary can show it.
    /// </summary>
    [TestMethod]
    public void AddLine_BeforeProtocolIsNegotiated_BuffersWithoutWriting()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        collector.AddLine("first", liveOutputStreamingState: null);
        collector.AddLine("second", liveOutputStreamingState: null);

        written.Should().BeEmpty();

        (string output, bool wasStreamedLive) = collector.GetCapturedOutput();
        wasStreamedLive.Should().BeFalse();
        output.Should().Be($"first{Environment.NewLine}second");
    }

    /// <summary>
    /// Protocol 1.0.0 hosts never stream, so the summary stays the only place their output is shown.
    /// Suppression must key off whether streaming actually happened, not off "a protocol was negotiated".
    /// </summary>
    [TestMethod]
    public void AddLine_WhenNegotiatedProtocolDoesNotSupportStreaming_BuffersWithoutWriting()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        collector.AddLine("first", liveOutputStreamingState: false);

        written.Should().BeEmpty();

        (string output, bool wasStreamedLive) = collector.GetCapturedOutput();
        wasStreamedLive.Should().BeFalse();
        output.Should().Be("first");
    }

    /// <summary>
    /// Once streaming engages the whole buffer is flushed, so every line the process produced has been
    /// shown and the capture must be reported as already streamed.
    /// </summary>
    [TestMethod]
    public void AddLine_WhenStreamingEngages_FlushesTheBufferOnceAndReportsCaptureAsStreamed()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        collector.AddLine("buffered-before-handshake", liveOutputStreamingState: null);
        collector.AddLine("first-streamed", liveOutputStreamingState: true);
        collector.AddLine("second-streamed", liveOutputStreamingState: true);

        string.Concat(written).Should().Be(
            $"buffered-before-handshake{Environment.NewLine}first-streamed{Environment.NewLine}second-streamed{Environment.NewLine}");

        collector.GetCapturedOutput().WasStreamedLive.Should().BeTrue();
    }

    /// <summary>
    /// The flush triggered by protocol negotiation covers output produced before the handshake even when
    /// the process writes nothing afterwards — the reader thread would otherwise never observe the
    /// transition.
    /// </summary>
    [TestMethod]
    public void FlushBufferedOutputIfLiveStreamingEnabled_WhenStreamingEngages_WritesTheBufferOnce()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        collector.AddLine("buffered-before-handshake", liveOutputStreamingState: null);

        collector.FlushBufferedOutputIfLiveStreamingEnabled(liveOutputStreamingState: true);
        collector.FlushBufferedOutputIfLiveStreamingEnabled(liveOutputStreamingState: true);

        written.Should().ContainSingle().Which.Should().Be($"buffered-before-handshake{Environment.NewLine}");
        collector.GetCapturedOutput().WasStreamedLive.Should().BeTrue();
    }

    /// <summary>
    /// Nothing is written for a process that produced no output, but the stream still counts as streamed
    /// so a later line is not mistaken for never-shown content.
    /// </summary>
    [TestMethod]
    public void FlushBufferedOutputIfLiveStreamingEnabled_WithEmptyBuffer_WritesNothingButEnablesStreaming()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        collector.FlushBufferedOutputIfLiveStreamingEnabled(liveOutputStreamingState: true);

        written.Should().BeEmpty();
        collector.GetCapturedOutput().WasStreamedLive.Should().BeTrue();
    }

    /// <summary>
    /// The capture is trimmed to a bounded tail once streaming engages, so it is a strict subset of what
    /// was already shown. That is exactly why the summary must not replay it.
    /// </summary>
    [TestMethod]
    public void AddLine_WhenStreaming_KeepsOnlyTheBoundedTailButStreamsEveryLine()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        for (int i = 1; i <= TailLineCount + 2; i++)
        {
            collector.AddLine($"line{i}", liveOutputStreamingState: true);
        }

        for (int i = 1; i <= TailLineCount + 2; i++)
        {
            string.Concat(written).Should().Contain($"line{i}");
        }

        (string output, bool wasStreamedLive) = collector.GetCapturedOutput();
        wasStreamedLive.Should().BeTrue();
        output.Should().Be($"line3{Environment.NewLine}line4{Environment.NewLine}line5");
    }

    /// <summary>
    /// The reader thread samples the streaming state before it takes the collector's lock, so the value
    /// it passes can be stale: the protocol-negotiation flush may have enabled streaming in between. The
    /// line still has to be written, because the capture is now reported as already shown and the summary
    /// suppresses it - skipping it would drop it from the terminal entirely.
    /// </summary>
    [TestMethod]
    public void AddLine_WithAStaleStreamingStateAfterStreamingEngaged_StillWritesTheLine()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        collector.FlushBufferedOutputIfLiveStreamingEnabled(liveOutputStreamingState: true);

        collector.AddLine("sampled-before-negotiation", liveOutputStreamingState: null);
        collector.AddLine("sampled-as-non-streaming", liveOutputStreamingState: false);

        string.Concat(written).Should().Be(
            $"sampled-before-negotiation{Environment.NewLine}sampled-as-non-streaming{Environment.NewLine}");

        collector.GetCapturedOutput().WasStreamedLive.Should().BeTrue();
    }

    /// <summary>
    /// The buffered lines have to reach the terminal in the order the process produced them. The flush
    /// (driven by protocol negotiation on the pipe thread) and the stdout reader run concurrently, so the
    /// write cannot happen after the collector's lock is released: a line added in that window would be
    /// printed ahead of the buffered lines that precede it. Live output is now the only copy the user
    /// sees, so nothing corrects a swap after the fact.
    /// </summary>
    [TestMethod]
    public void AddLine_WhileTheNegotiationFlushIsWriting_CannotOvertakeTheBufferedLines()
    {
        var written = new List<string>();
        TestApplication.ProcessOutputCollector? collector = null;
        Thread? readerThread = null;
        int firstWrite = 0;

        collector = new TestApplication.ProcessOutputCollector(liveOutputTailLineCount: 1000, line =>
        {
            // Reproduce the reader thread arriving exactly while the flush is writing. If the write is
            // not covered by the collector's lock, this AddLine runs to completion here and its line
            // lands before the flush records its own.
            if (Interlocked.Exchange(ref firstWrite, 1) == 0)
            {
                readerThread = new Thread(() => collector!.AddLine("produced-second", liveOutputStreamingState: true));
                readerThread.Start();
                readerThread.Join(TimeSpan.FromMilliseconds(250));
            }

            lock (written)
            {
                written.Add(line);
            }
        });

        collector.AddLine("produced-first", liveOutputStreamingState: null);
        collector.FlushBufferedOutputIfLiveStreamingEnabled(liveOutputStreamingState: true);

        readerThread.Should().NotBeNull();
        readerThread!.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();

        string rendered = string.Concat(written);
        rendered.IndexOf("produced-first", StringComparison.Ordinal)
            .Should().BeLessThan(
                rendered.IndexOf("produced-second", StringComparison.Ordinal),
                "live output must keep the order the test process produced it in");
    }
}

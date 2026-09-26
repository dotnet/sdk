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

    public TestContext TestContext { get; set; } = null!;

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
        collector.GetOutputToReport().Should().Be($"first{Environment.NewLine}second");
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
        collector.GetOutputToReport().Should().Be("first");
    }

    /// <summary>
    /// Once streaming engages the whole buffer is flushed, so every line the process produced has been
    /// shown and nothing is left for the caller to report.
    /// </summary>
    [TestMethod]
    public void AddLine_WhenStreamingEngages_FlushesTheBufferOnceAndReportsNothing()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        collector.AddLine("buffered-before-handshake", liveOutputStreamingState: null);
        collector.AddLine("first-streamed", liveOutputStreamingState: true);
        collector.AddLine("second-streamed", liveOutputStreamingState: true);

        string.Concat(written).Should().Be(
            $"buffered-before-handshake{Environment.NewLine}first-streamed{Environment.NewLine}second-streamed{Environment.NewLine}");

        collector.GetOutputToReport().Should().BeEmpty();
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
        collector.GetOutputToReport().Should().BeEmpty();
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

        collector.AddLine("produced-after-the-flush", liveOutputStreamingState: true);
        string.Concat(written).Should().Be($"produced-after-the-flush{Environment.NewLine}");
        collector.GetOutputToReport().Should().BeEmpty();
    }

    /// <summary>
    /// The capture is trimmed to a bounded tail once streaming engages, so it is a strict subset of what
    /// was already shown. That is exactly why there is nothing left to report.
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

        collector.GetOutputToReport().Should().BeEmpty();
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

        collector.GetOutputToReport().Should().BeEmpty();
    }

    /// <summary>
    /// The diagnostics accessor keeps returning the capture after it was streamed. The trace file has no
    /// console sink and is collected on its own, so it would otherwise silently lose output that only ever
    /// went to the terminal.
    /// </summary>
    [TestMethod]
    public void GetCapturedOutput_AfterStreamingEngaged_StillReturnsTheCapture()
    {
        var written = new List<string>();
        var collector = new TestApplication.ProcessOutputCollector(TailLineCount, written.Add);

        collector.AddLine("streamed-line", liveOutputStreamingState: true);

        collector.GetOutputToReport().Should().BeEmpty("it is already on the terminal");
        collector.GetCapturedOutput().Should().Be("streamed-line", "diagnostics still need the text");
    }

    /// <summary>
    /// The buffered lines have to reach the terminal in the order the process produced them. The flush
    /// (driven by protocol negotiation on the pipe thread) and the stdout reader run concurrently, so the
    /// write cannot happen once the ordering lock is released: a line added in that window would be
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

        using var readerEnteredAddLine = new ManualResetEventSlim(false);
        using var readerCompletedAddLine = new ManualResetEventSlim(false);

        collector = new TestApplication.ProcessOutputCollector(liveOutputTailLineCount: 1000, line =>
        {
            // Reproduce the reader thread arriving exactly while the flush is writing. If the write is
            // not covered by the ordering lock, that AddLine runs to completion here and its line lands
            // before the flush records its own.
            if (Interlocked.Exchange(ref firstWrite, 1) == 0)
            {
                readerThread = new Thread(() =>
                {
                    readerEnteredAddLine.Set();
                    collector!.AddLine("produced-second", liveOutputStreamingState: true);
                    readerCompletedAddLine.Set();
                })
                {
                    // Background so a thread left blocked by a regression cannot keep the test host alive.
                    IsBackground = true,
                };
                readerThread.Start();

                // Wait for the reader to be running and about to call AddLine, so the probe below is not
                // measuring thread start-up. Then give it a window to get through AddLine: it must not,
                // because this callback still holds the ordering lock.
                readerEnteredAddLine.Wait(TimeSpan.FromSeconds(30), TestContext.CancellationToken).Should().BeTrue();
                readerCompletedAddLine.Wait(TimeSpan.FromSeconds(2), TestContext.CancellationToken)
                    .Should().BeFalse("a line produced while the flush is writing must wait for it");
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

    /// <summary>
    /// Reading the capture must not wait on terminal IO. <c>TestApplication.RunAsync</c> gives the reader
    /// threads a bounded grace period after the test process exits and then reads the capture anyway,
    /// precisely because a reader can still be stuck; that timeout would be pointless if the read could
    /// then block behind an in-flight write.
    /// </summary>
    [TestMethod]
    public void GetOutputToReport_WhileALineIsBeingWritten_DoesNotWaitForTheWriteToComplete()
    {
        TestApplication.ProcessOutputCollector? collector = null;

        using var writeStarted = new ManualResetEventSlim(false);
        using var releaseWrite = new ManualResetEventSlim(false);

        CancellationToken cancellationToken = TestContext.CancellationToken;

        collector = new TestApplication.ProcessOutputCollector(TailLineCount, _ =>
        {
            writeStarted.Set();
            releaseWrite.Wait(TimeSpan.FromSeconds(30), cancellationToken);
        });

        var writerThread = new Thread(() => collector.AddLine("blocked-line", liveOutputStreamingState: true))
        {
            // Background so a thread left blocked by a regression cannot keep the test host alive.
            IsBackground = true,
        };
        writerThread.Start();

        try
        {
            writeStarted.Wait(TimeSpan.FromSeconds(30), cancellationToken).Should().BeTrue();

            // The read runs on its own thread so a regression that puts it behind the blocked write shows
            // up as a failed join here. Reading on this thread instead would deadlock until the callback's
            // own timeout released it, and the test would then still pass - just far more slowly.
            string? output = null;
            var captureThread = new Thread(() => output = collector.GetOutputToReport())
            {
                // Background so a thread left blocked by a regression cannot keep the test host alive.
                IsBackground = true,
            };
            captureThread.Start();

            captureThread.Join(TimeSpan.FromSeconds(15))
                .Should().BeTrue("reading the capture must not wait for an in-flight terminal write");

            output.Should().BeEmpty("the line being written is already reaching the terminal");
        }
        finally
        {
            releaseWrite.Set();
            writerThread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();
        }
    }
}

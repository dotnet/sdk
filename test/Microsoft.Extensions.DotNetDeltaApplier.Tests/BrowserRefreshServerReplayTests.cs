// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Net.WebSockets;

namespace Microsoft.DotNet.HotReload.UnitTests;

/// <summary>
/// Covers the replay invariants the redesign relies on. Replay used to be an HTTP fetch keyed by a
/// generation id; it is now serialized on the authenticated WebSocket, so the store has to
/// guarantee by itself that every browser applies every batch exactly once and in order.
/// </summary>
[TestClass]
public class BrowserRefreshServerReplayTests
{
    private static BrowserToolsUpdateBatch Batch(Guid moduleId)
        => new([new BrowserToolsManagedCodeUpdate(moduleId, [1], [2], [3], [4])]);

    private static ValueTask<bool> SendAsync(TestBrowserRefreshServer server, int epoch, BrowserToolsUpdateBatch batch)
        => server.SendManagedCodeUpdateAsync(epoch, batch, static _ => new byte[] { 1 }.AsMemory(), CancellationToken.None);

    /// <summary>
    /// An update produced while no browser is connected is retained and replayed to the next one.
    /// </summary>
    [TestMethod]
    public async Task UpdateProducedBeforeConnect_IsReplayedAndNotSentLive()
    {
        using var server = new TestBrowserRefreshServer();
        var moduleId = Guid.NewGuid();

        await SendAsync(server, epoch: 0, Batch(moduleId));

        Assert.HasCount(1, server.LiveDeliveries);
        Assert.IsEmpty(server.LiveDeliveries[0]);

        using var socket = new TestWebSocket();
        var connection = server.Connect(socket);

        Assert.AreSequenceEqual(new[] { moduleId }, connection.PendingReplayUpdates.Select(b => b.Deltas.Single().ModuleId).ToArray());
    }

    /// <summary>
    /// An update produced after a browser connected is delivered live and must not also appear in
    /// that browser's replay snapshot, otherwise it would be applied twice.
    /// </summary>
    [TestMethod]
    public async Task UpdateProducedAfterConnect_IsSentLiveAndNotReplayed()
    {
        using var server = new TestBrowserRefreshServer();

        using var socket = new TestWebSocket();
        var connection = server.Connect(socket);

        Assert.IsEmpty(connection.PendingReplayUpdates);

        await SendAsync(server, epoch: 0, Batch(Guid.NewGuid()));

        Assert.HasCount(1, server.LiveDeliveries);
        Assert.AreSequenceEqual(new[] { connection.Id }, server.LiveDeliveries[0]);
    }

    /// <summary>
    /// Replay preserves production order: the browser applies deltas in the order they were produced.
    /// </summary>
    [TestMethod]
    public async Task RetainedUpdates_PreserveOrder()
    {
        using var server = new TestBrowserRefreshServer();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await SendAsync(server, epoch: 0, Batch(first));
        await SendAsync(server, epoch: 0, Batch(second));

        using var socket = new TestWebSocket();
        var connection = server.Connect(socket);

        Assert.AreSequenceEqual(new[] { first, second }, connection.PendingReplayUpdates.Select(b => b.Deltas.Single().ModuleId).ToArray());
    }

    /// <summary>
    /// A full rebuild discards the previous baseline: retained updates are cleared and the
    /// connections bound to the old baseline are closed, so a browser can never mix baselines.
    /// </summary>
    [TestMethod]
    public async Task ResetUpdates_ClearsRetainedUpdatesAndClosesConnections()
    {
        using var server = new TestBrowserRefreshServer();

        using var socket = new TestWebSocket();
        var connection = server.Connect(socket);

        await SendAsync(server, epoch: 0, Batch(Guid.NewGuid()));
        Assert.HasCount(1, server.GetRetainedUpdates());

        var epoch = server.ResetUpdates();

        Assert.AreEqual(1, epoch);
        Assert.IsEmpty(server.GetRetainedUpdates());
        Assert.IsTrue(connection.Disconnected.Task.IsCompleted);
        Assert.IsFalse(await connection.Initialized.Task);

        using var socket2 = new TestWebSocket();
        Assert.IsEmpty(server.Connect(socket2).PendingReplayUpdates);
    }

    /// <summary>
    /// The internal epoch is the only reason a generation identity survives the redesign: an
    /// in flight update produced by the client of a superseded baseline must be dropped rather than
    /// appended to the new baseline's replay state.
    /// </summary>
    [TestMethod]
    public async Task StaleEpochUpdate_IsDropped()
    {
        using var server = new TestBrowserRefreshServer();
        var epoch = server.ResetUpdates();

        Assert.IsTrue(await SendAsync(server, epoch - 1, Batch(Guid.NewGuid())));

        Assert.IsEmpty(server.GetRetainedUpdates());
        Assert.IsEmpty(server.LiveDeliveries);

        await SendAsync(server, epoch, Batch(Guid.NewGuid()));
        Assert.HasCount(1, server.GetRetainedUpdates());
    }

    /// <summary>
    /// Closed sockets are not considered for live delivery, but a browser that reconnects still
    /// replays the retained batches.
    /// </summary>
    [TestMethod]
    public async Task ClosedConnection_IsNotSentLiveButUpdateIsStillRetained()
    {
        using var server = new TestBrowserRefreshServer();

        using var socket = new TestWebSocket();
        server.Connect(socket);
        socket.Close();

        await SendAsync(server, epoch: 0, Batch(Guid.NewGuid()));

        Assert.HasCount(1, server.LiveDeliveries);
        Assert.IsEmpty(server.LiveDeliveries[0]);
        Assert.HasCount(1, server.GetRetainedUpdates());
    }

    private sealed class TestWebSocket : WebSocket
    {
        private WebSocketState _state = WebSocketState.Open;

        public void Close() => _state = WebSocketState.Closed;

        public override WebSocketState State => _state;
        public override void Abort() => Close();
        public override void Dispose() => Close();

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}

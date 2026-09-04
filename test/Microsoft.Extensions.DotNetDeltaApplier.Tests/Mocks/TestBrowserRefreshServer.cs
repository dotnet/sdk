// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.WebSockets;
using System.Text;
using Microsoft.DotNet.HotReload;
using Microsoft.DotNet.Test.MSTest.Utilities;

namespace Microsoft.DotNet.HotReload.UnitTests;

internal sealed class TestBrowserRefreshServer()
    : AbstractBrowserRefreshServer(configureLaunchEnvironment: static (_, _) => { }, new SharedSecretProvider(), new TestLogger(), _ => new TestLogger(), _ => new TestLogger())
{
    /// <summary>
    /// The invocation scoped key pair. Tests use its public half to build encrypted sub-protocols.
    /// </summary>
    public SharedSecretProvider Key => SessionKey;

    public List<string> SentMessages { get; } = [];

    /// <summary>
    /// The connections captured for live delivery of each successfully appended update batch.
    /// </summary>
    public List<int[]> LiveDeliveries { get; } = [];

    /// <summary>
    /// Publishes a connection the way <see cref="AcceptBrowserConnectionAsync"/> does, without
    /// requiring a real socket upgrade.
    /// </summary>
    public BrowserConnection Connect(WebSocket socket)
        => OnBrowserConnected(socket, sharedSecret: "test-secret");

    internal override ValueTask<TResult?> SendAndReceiveAsync<TRequest, TResult>(
        IReadOnlyCollection<BrowserConnection> openConnections,
        Func<string?, TRequest>? request,
        ResponseFunc<TResult>? response,
        CancellationToken cancellationToken)
        where TResult : struct
    {
        LiveDeliveries.Add([.. openConnections.Select(c => c.Id)]);

        if (request != null)
        {
            var requestValue = request(null);
            var requestBytes = requestValue is ReadOnlyMemory<byte> bytes ? bytes : SerializeJson(requestValue);
            SentMessages.Add(Encoding.UTF8.GetString(requestBytes.Span));
        }

        return ValueTask.FromResult<TResult?>(null);
    }

    protected override ValueTask<WebServerHost> CreateAndStartHostAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    protected override bool SuppressTimeouts => true;
}

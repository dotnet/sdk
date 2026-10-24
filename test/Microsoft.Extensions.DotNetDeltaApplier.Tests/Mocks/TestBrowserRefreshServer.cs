// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.DotNet.HotReload;
using Microsoft.DotNet.Test.MSTest.Utilities;

namespace Microsoft.DotNet.HotReload.UnitTests;

internal sealed class TestBrowserRefreshServer : AbstractBrowserRefreshServer
{
    private readonly SharedSecretProvider _key;

    public TestBrowserRefreshServer()
        : this(CreateKeyParameters())
    {
    }

    public TestBrowserRefreshServer(Func<SharedSecretProvider> sessionKeyFactory)
        : base(
            configureLaunchEnvironment: static (_, _) => { },
            sessionKeyFactory,
            new TestLogger(),
            _ => new TestLogger(),
            _ => new TestLogger())
    {
        _key = new SharedSecretProvider();
    }

    private TestBrowserRefreshServer(RSAParameters keyParameters)
        : base(
            configureLaunchEnvironment: static (_, _) => { },
            sessionKeyFactory: () => new SharedSecretProvider(keyParameters),
            new TestLogger(),
            _ => new TestLogger(),
            _ => new TestLogger())
    {
        _key = new SharedSecretProvider(keyParameters);
    }

    /// <summary>
    /// The key pair the provider is keyed with. Tests use its public half to build encrypted
    /// sub-protocols. In production it comes from the project's build output.
    /// </summary>
    public SharedSecretProvider Key => _key;

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

    public override void Dispose()
    {
        base.Dispose();
        _key.Dispose();
    }

    private static RSAParameters CreateKeyParameters()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(includePrivateParameters: true);
    }
}

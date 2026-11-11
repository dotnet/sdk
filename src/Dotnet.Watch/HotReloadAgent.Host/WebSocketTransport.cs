// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// WebSocket-based client for hot reload communication.
/// Used for projects with the HotReloadWebSockets capability (e.g., Android, iOS).
/// Mobile workloads add this capability since named pipes don't work over the network.
/// Uses RSA-based shared secret for authentication (same as BrowserRefreshServer).
/// </summary>
internal sealed class WebSocketTransport(string serverUrl, string serverPublicKey, Action<string> log, int connectionTimeoutMS)
    : Transport(log)
{
    private readonly ClientWebSocket _webSocket = new();

    // Buffers for WebSocket messages - reused across calls to avoid allocations.
    // SendAsync is invoked under a lock after the first message, so _sendBuffer is safe to reuse.
    private MemoryStream? _sendBuffer;
    private MemoryStream? _receiveBuffer;

    public override void Dispose()
    {
        _webSocket.Dispose();
        _sendBuffer?.Dispose();
        _receiveBuffer?.Dispose();
    }

    public override string DisplayName
        => $"WebSocket {serverUrl}";

    public override async ValueTask SendAsync(IResponse response, CancellationToken cancellationToken)
    {
        // Connect on first send (which is InitializationResponse)
        if (response.Type == ResponseType.InitializationResponse)
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(connectionTimeoutMS);

            try
            {
                // Add encrypted shared secret as subprotocol for authentication
                var encryptedSecret = EncryptSharedSecret(serverPublicKey, out var sharedSecret);
                _webSocket.Options.AddSubProtocol(encryptedSecret);

                var serverUri = new Uri(serverUrl);
                _webSocket.Options.SetRequestHeader("Origin", serverUri.Scheme + "://localhost");

                Log($"Connecting to {serverUrl}...");
                await _webSocket.ConnectAsync(serverUri, connectCts.Token).ConfigureAwait(false);
                await VerifyServerAsync(sharedSecret, connectCts.Token).ConfigureAwait(false);
                Log("Connected.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Failed to connect in {connectionTimeoutMS}ms.");
            }
        }

        // Serialize the response to a reusable buffer
        _sendBuffer ??= new MemoryStream();
        _sendBuffer.SetLength(0);

        await _sendBuffer.WriteAsync((byte)response.Type, cancellationToken).ConfigureAwait(false);
        await response.WriteAsync(_sendBuffer, cancellationToken).ConfigureAwait(false);

        Log($"Sending {response.Type} ({_sendBuffer.Length} bytes)");

        // Send as binary WebSocket message
        await _webSocket.SendAsync(
            new ArraySegment<byte>(_sendBuffer.GetBuffer(), 0, (int)_sendBuffer.Length),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    public override ValueTask<RequestStream> ReceiveAsync(CancellationToken cancellationToken)
        => ReceiveAsync(expectedMessageType: null, maxMessageLength: null, cancellationToken);

    private async ValueTask<RequestStream> ReceiveAsync(WebSocketMessageType? expectedMessageType, int? maxMessageLength, CancellationToken cancellationToken)
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            return new RequestStream(stream: null, disposeOnCompletion: false);
        }

        // Read the complete WebSocket message into a buffer
        _receiveBuffer ??= new MemoryStream();
        _receiveBuffer.SetLength(0);

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            WebSocketReceiveResult result;
            do
            {
                var receiveCount = maxMessageLength.HasValue
                    ? (int)Math.Min(buffer.Length, maxMessageLength.Value + 1L - _receiveBuffer.Length)
                    : buffer.Length;
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, receiveCount), cancellationToken).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log("Server closed connection.");
                    return new RequestStream(stream: null, disposeOnCompletion: false);
                }

                if ((expectedMessageType.HasValue && result.MessageType != expectedMessageType.Value) ||
                    (maxMessageLength.HasValue && _receiveBuffer.Length + result.Count > maxMessageLength.Value))
                {
                    throw new InvalidDataException("The Hot Reload provider returned an invalid authentication response.");
                }

                _receiveBuffer.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        Log($"Received {_receiveBuffer.Length} bytes");
        _receiveBuffer.Position = 0;

        // Return a stream that doesn't dispose the underlying buffer (we reuse it)
        return new RequestStream(_receiveBuffer, disposeOnCompletion: false);
    }

    /// <summary>
    /// Encrypts a random shared secret using the server's RSA public key.
    /// Uses the same algorithm as BrowserRefreshServer for consistency.
    /// </summary>
    private static string EncryptSharedSecret(string serverPublicKeyBase64, out byte[] sharedSecret)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(serverPublicKeyBase64), out _);

        sharedSecret = RandomNumberGenerator.GetBytes(32);
        var encrypted = rsa.Encrypt(sharedSecret, RSAEncryptionPadding.OaepSHA256);

        // URL-encode standard Base64 for WebSocket subprotocol header (same encoding as BrowserRefreshServer)
        return WebUtility.UrlEncode(Convert.ToBase64String(encrypted));
    }

    private async Task VerifyServerAsync(byte[] sharedSecret, CancellationToken cancellationToken)
    {
        // This size is safe for the stackalloc below.
        Debug.Assert(sharedSecret.Length == 32);

        var encodedLength = Base64.GetMaxEncodedToUtf8Length(sharedSecret.Length);
        using var response = await ReceiveAsync(WebSocketMessageType.Text, encodedLength, cancellationToken).ConfigureAwait(false);
        if (response.Stream == null)
        {
            throw new InvalidDataException("The Hot Reload provider closed the connection before authentication.");
        }

        Span<byte> expectedSecret = stackalloc byte[encodedLength];
        Base64.EncodeToUtf8(sharedSecret, expectedSecret, out _, out _);
        Debug.Assert(_receiveBuffer != null);
        if (!_receiveBuffer.GetBuffer().AsSpan(0, (int)_receiveBuffer.Length).SequenceEqual(expectedSecret))
        {
            throw new InvalidDataException("The Hot Reload provider did not return the expected shared secret.");
        }
    }
}

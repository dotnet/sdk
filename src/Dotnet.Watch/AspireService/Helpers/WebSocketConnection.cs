// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;

namespace Aspire.Tools.Service;

/// <summary>
/// Used by the SocketConnectionManager to track one socket connection. It needs to be disposed when done with it
/// </summary>
internal sealed class WebSocketConnection(WebSocket socket, TaskCompletionSource tcs, string dcpId, CancellationToken httpRequestAborted) : IDisposable
{
    public WebSocket Socket { get; } = socket;
    public TaskCompletionSource Tcs { get; } = tcs;
    public string DcpId { get; } = dcpId;
    public CancellationToken HttpRequestAborted { get; } = httpRequestAborted;

    private readonly Lock _cancelTokenRegistrationLock = new();
    private CancellationTokenRegistration? _cancelTokenRegistration;
    private bool _isDisposed;

    public void Dispose()
    {
        Tcs.TrySetResult();

        CancellationTokenRegistration? registrationToDispose = null;
        lock (_cancelTokenRegistrationLock)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                registrationToDispose = _cancelTokenRegistration;
                _cancelTokenRegistration = null;
            }
        }

        // The callback might be called during disposal, do so outside of the lock:
        registrationToDispose?.Dispose();
    }

    public void RegisterCancellationCallback(Action<WebSocketConnection> callback)
    {
        // Note that the callback can be called synchronously before Register returns.
        var cancelTokenRegistration = HttpRequestAborted.Register(() => callback(this));

        bool disposeRegistration;
        lock (_cancelTokenRegistrationLock)
        {
            disposeRegistration = _isDisposed;

            if (!disposeRegistration)
            {
                _cancelTokenRegistration = cancelTokenRegistration;
            }
        }

        if (disposeRegistration)
        {
            // The callback might be called during disposal, do so outside of the lock:
            cancelTokenRegistration.Dispose();
        }
    }
}

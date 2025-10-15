﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WebTools.AspireServer;

/// <summary>
/// Used by the SocketConnectionManager to track one socket connection. It needs to be disposed when done with it
/// </summary>
internal class WebSocketConnection : IDisposable
{
    public WebSocketConnection(WebSocket socket, TaskCompletionSource tcs, string dcpId, CancellationToken httpRequestAborted)
    {
        Socket = socket;
        Tcs = tcs;
        DcpId = dcpId;
        HttpRequestAborted = httpRequestAborted;
    }

    public WebSocket Socket { get; }
    public TaskCompletionSource Tcs { get; }
    public string DcpId { get; }
    public CancellationToken HttpRequestAborted { get; }
    public CancellationTokenRegistration CancelTokenRegistration { get; set; }

    public void Dispose()
    {
       Tcs.SetResult();
       CancelTokenRegistration.Dispose();
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.

#nullable enable

using System.Threading;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// Abstraction that simplifies using <see cref="CancellationTokenSource"/> that triggers on disposal.
/// </summary>
internal sealed class DisposalCancellationSource : CancellationTokenSource
{
    private int _isDisposed;

    /// <summary>
    /// Token is stored at construction time so that it can be used even after the source has been disposed.
    /// </summary>
    public new readonly CancellationToken Token;

    public DisposalCancellationSource()
        => Token = base.Token;

    public CancellationTokenSource Link(CancellationToken cancellationToken)
        => CreateLinkedTokenSource(cancellationToken, Token);

    public void CancelAndDispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            Cancel();
            Dispose();
        }
    }
}

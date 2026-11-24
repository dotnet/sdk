// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

#nullable enable

using Microsoft.VisualStudio.Threading;

namespace System.Threading.Tasks;

internal static partial class TaskExtensions
{
    extension<T>(Task task)
    {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        public Task WaitAsync(CancellationToken cancellationToken)
            => task.WithCancellation(cancellationToken);
#pragma warning restore VSTHRD003
    }

    extension<T>(Task<T> task)
    {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        public Task<T> WaitAsync(CancellationToken cancellationToken)
            => task.WithCancellation(cancellationToken);
#pragma warning restore VSTHRD003
    }

    extension(CancellationTokenSource source)
    {
        public async ValueTask CancelAsync()
            => source.Cancel();
    }
}

#endif

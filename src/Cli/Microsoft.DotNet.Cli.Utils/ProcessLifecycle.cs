// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

internal static class ProcessLifecycle
{
    private static readonly CancellationTokenSource s_cancellationTokenSource = new();

    static ProcessLifecycle()
    {
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            if (!s_cancellationTokenSource.IsCancellationRequested)
            {
                eventArgs.Cancel = true;
                s_cancellationTokenSource.Cancel();
            }
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => s_cancellationTokenSource.Cancel();
    }

    public static CancellationToken CancellationToken => s_cancellationTokenSource.Token;
}

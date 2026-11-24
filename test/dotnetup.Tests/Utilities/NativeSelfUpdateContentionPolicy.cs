// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class NativeSelfUpdateContentionPolicy(ManualResetEventSlim contended) : LockFileRetryPolicy(TimeSpan.FromSeconds(30))
{
    public override void WaitBeforeRetry(int attempt, TimeSpan remaining, CancellationToken cancellationToken)
    {
        contended.Set();
        base.WaitBeforeRetry(attempt, remaining, cancellationToken);
    }
}
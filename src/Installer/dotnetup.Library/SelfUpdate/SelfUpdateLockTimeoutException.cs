// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Identifies which contention budget expired, leaving command error mapping to the caller.
/// </summary>
internal sealed class SelfUpdateLockTimeoutException : TimeoutException
{
    public SelfUpdateLockTimeoutException()
    {
    }

    public SelfUpdateLockTimeoutException(string? message) : base(message)
    {
    }

    public SelfUpdateLockTimeoutException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public SelfUpdateLockTimeoutException(SelfUpdateLockKind lockKind, string lockPath)
        : base($"Timed out waiting for the {lockKind} lock '{lockPath}'.")
    {
        LockKind = lockKind;
        LockPath = lockPath;
    }

    public SelfUpdateLockKind? LockKind { get; }

    public string? LockPath { get; }
}
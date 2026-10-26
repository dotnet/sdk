// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Reports that the executable's location or name is unsupported for self-update coordination
/// (for example a link, reparse point, or non-canonical name) rather than an I/O failure.
/// </summary>
/// <remarks>
/// Derives from <see cref="IOException"/> so replacement and recovery code that treats path
/// validation failures as file failures keeps its existing behavior; command entry points map it
/// to a specific user-facing <see cref="DotnetInstallErrorCode"/>.
/// </remarks>
internal sealed class SelfUpdateLocationException : IOException
{
    public SelfUpdateLocationException()
    {
    }

    public SelfUpdateLocationException(string? message) : base(message)
    {
    }

    public SelfUpdateLocationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public SelfUpdateLocationException(DotnetInstallErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DotnetInstallErrorCode ErrorCode { get; } = DotnetInstallErrorCode.DotnetupUnsupportedInstallLocation;

    public DotnetInstallException ToInstallException() => new(ErrorCode, Message, this);
}

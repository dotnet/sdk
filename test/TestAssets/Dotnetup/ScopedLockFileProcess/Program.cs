// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using Microsoft.Dotnet.Installation.Internal;

using var lease = args[0] == "shared"
    ? ScopedLockFile.TryAcquireShared(args[1])
    : ScopedLockFile.TryAcquireExclusive(args[1]);

if (lease is null)
{
    return 2;
}

if (args.Length == 3)
{
    using var pipe = new NamedPipeServerStream(args[2], PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
    pipe.WaitForConnection();
    pipe.ReadByte();
}

return 0;
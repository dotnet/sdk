// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

internal sealed class WaitForServerControlRequest : IRequest
{
    public static readonly WaitForServerControlRequest CachedInstance = new();
}

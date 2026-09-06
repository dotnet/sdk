// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal sealed class WaitForServerControlRequestSerializer : INamedPipeSerializer
{
    public int Id => WaitForServerControlRequestFieldsId.MessagesSerializerId;

    public object Deserialize(Stream _)
        => WaitForServerControlRequest.CachedInstance;

    public void Serialize(object _, Stream __)
    {
    }
}

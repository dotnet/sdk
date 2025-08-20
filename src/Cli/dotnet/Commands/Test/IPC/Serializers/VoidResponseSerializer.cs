// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal sealed class VoidResponseSerializer : INamedPipeSerializer
{
    public int Id => VoidResponseFieldsId.MessagesSerializerId;

    public object Deserialize(Stream _)
        => new VoidResponse();

    public void Serialize(object _, Stream __)
    {
    }
}

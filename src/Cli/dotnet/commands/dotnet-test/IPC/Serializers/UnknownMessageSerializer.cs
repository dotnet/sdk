// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test;

internal sealed class UnknownMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id { get; }

    public UnknownMessageSerializer(int SerializerId) => Id = SerializerId;

    public object Deserialize(Stream _)
    {
        return new UnknownMessage(Id);
    }

    public void Serialize(object _, Stream stream)
    {
        WriteInt(stream, Id);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test;

internal sealed class UnknownMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => -1;

    public int SerializerId { get; }

    public UnknownMessageSerializer(int SerializerId) => this.SerializerId = SerializerId;

    public object Deserialize(Stream _)
    {
        return new UnknownMessage(SerializerId);
    }

    public void Serialize(object _, Stream stream)
    {
        WriteInt(stream, SerializerId);
    }
}

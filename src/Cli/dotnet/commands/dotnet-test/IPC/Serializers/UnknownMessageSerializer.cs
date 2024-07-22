// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test;

internal sealed class UnknownMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 100;

    public object Deserialize(Stream stream)
    {
        int serializerId = ReadInt(stream);
        return new UnknownMessage(serializerId);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        WriteInt(stream, ((UnknownMessage)objectToSerialize).SerializerId);
    }
}

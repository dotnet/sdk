// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal sealed class HandshakeMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => HandshakeMessageFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        Dictionary<byte, string> properties = [];

        ushort fieldCount = ReadShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            properties.Add(ReadByte(stream), ReadString(stream));
        }

        return new HandshakeMessage(properties);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var handshakeMessage = (HandshakeMessage)objectToSerialize;

        if (handshakeMessage.Properties is null || handshakeMessage.Properties.Count == 0)
        {
            return;
        }

        WriteShort(stream, (ushort)handshakeMessage.Properties.Count);
        foreach (KeyValuePair<byte, string> property in handshakeMessage.Properties)
        {
            WriteField(stream, property.Key);
            WriteField(stream, property.Value);
        }
    }
}

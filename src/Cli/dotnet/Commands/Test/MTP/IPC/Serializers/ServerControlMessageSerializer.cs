// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal sealed class ServerControlMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => ServerControlMessageFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        byte kind = 0;
        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            if (fieldId == ServerControlMessageFieldsId.Kind)
            {
                kind = ReadByte(stream);
                if (fieldSize > 1)
                {
                    SetPosition(stream, stream.Position + fieldSize - 1);
                }
            }
            else
            {
                SetPosition(stream, stream.Position + fieldSize);
            }
        }

        return new ServerControlMessage(kind);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var message = (ServerControlMessage)objectToSerialize;
        WriteUShort(stream, 1);
        WriteField(stream, ServerControlMessageFieldsId.Kind, message.Kind);
    }
}

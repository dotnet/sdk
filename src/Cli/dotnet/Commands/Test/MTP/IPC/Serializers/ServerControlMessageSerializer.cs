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
        ReadFields(stream, (fieldId, _) =>
        {
            switch (fieldId)
            {
                case ServerControlMessageFieldsId.Kind:
                    kind = ReadByte(stream);
                    return true;

                default:
                    return false;
            }
        });

        return new ServerControlMessage(kind);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var message = (ServerControlMessage)objectToSerialize;
        WriteUShort(stream, 1);
        WriteField(stream, ServerControlMessageFieldsId.Kind, message.Kind);
    }
}

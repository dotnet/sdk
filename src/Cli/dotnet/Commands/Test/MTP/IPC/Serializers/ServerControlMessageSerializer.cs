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
        int? aggregateExitCode = null;
        bool? isCanceled = null;
        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case ServerControlMessageFieldsId.Kind:
                    kind = ReadByte(stream);
                    return true;

                case ServerControlMessageFieldsId.AggregateExitCode:
                    aggregateExitCode = ReadFieldPayload(stream, fieldSize, ReadInt);
                    return true;

                case ServerControlMessageFieldsId.IsCanceled:
                    isCanceled = ReadFieldPayload(stream, fieldSize, ReadBool);
                    return true;

                default:
                    return false;
            }
        });

        return new ServerControlMessage(kind, aggregateExitCode, isCanceled);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var message = (ServerControlMessage)objectToSerialize;
        WriteUShort(stream, (ushort)(1
            + (message.AggregateExitCode is null ? 0 : 1)
            + (message.IsCanceled is null ? 0 : 1)));
        WriteField(stream, ServerControlMessageFieldsId.Kind, message.Kind);
        WriteField(stream, ServerControlMessageFieldsId.AggregateExitCode, message.AggregateExitCode);
        WriteField(stream, ServerControlMessageFieldsId.IsCanceled, message.IsCanceled);
    }
}
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

/*
    |---FieldCount---| 2 bytes

    |---ExecutionId Id---| (2 bytes)
    |---ExecutionId Size---| (4 bytes)
    |---ExecutionId Value---| (n bytes)

    |---InstanceId Id---| (2 bytes)
    |---InstanceId Size---| (4 bytes)
    |---InstanceId Value---| (n bytes)

    |---Level Id---| (2 bytes)
    |---Level Size---| (4 bytes)
    |---Level Value---| (1 byte)

    |---Text Id---| (2 bytes)
    |---Text Size---| (4 bytes)
    |---Text Value---| (n bytes)
*/

internal sealed class DisplayMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => DisplayMessageFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        byte level = DisplayMessageLevels.Information;
        string? text = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case DisplayMessageFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    return true;

                case DisplayMessageFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    return true;

                case DisplayMessageFieldsId.Level:
                    level = ReadByte(stream);
                    return true;

                case DisplayMessageFieldsId.Text:
                    text = ReadStringValue(stream, fieldSize);
                    return true;

                default:
                    return false;
            }
        });

        return new DisplayMessage(executionId, instanceId, level, text);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var message = (DisplayMessage)objectToSerialize;

        WriteUShort(stream, GetFieldCount(message));

        WriteField(stream, DisplayMessageFieldsId.ExecutionId, message.ExecutionId);
        WriteField(stream, DisplayMessageFieldsId.InstanceId, message.InstanceId);
        WriteField(stream, DisplayMessageFieldsId.Level, message.Level);
        WriteField(stream, DisplayMessageFieldsId.Text, message.Text);
    }

    // Level is always written (it is a non-nullable byte); the two id strings and the text are optional.
    private static ushort GetFieldCount(DisplayMessage message) =>
        (ushort)((message.ExecutionId is null ? 0 : 1) +
        (message.InstanceId is null ? 0 : 1) +
        1 +
        (message.Text is null ? 0 : 1));
}

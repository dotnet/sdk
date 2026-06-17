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

    |---Text Id---| (2 bytes)
    |---Text Size---| (4 bytes)
    |---Text Value---| (n bytes)
*/

internal sealed class TestHostOutputDeviceMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => TestHostOutputDeviceMessageFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        string? text = null;

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case TestHostOutputDeviceMessageFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                case TestHostOutputDeviceMessageFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    break;

                case TestHostOutputDeviceMessageFieldsId.Text:
                    text = ReadStringValue(stream, fieldSize);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new TestHostOutputDeviceMessage(executionId, instanceId, text);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var testHostOutputDeviceMessage = (TestHostOutputDeviceMessage)objectToSerialize;

        WriteUShort(stream, GetFieldCount(testHostOutputDeviceMessage));

        WriteField(stream, TestHostOutputDeviceMessageFieldsId.ExecutionId, testHostOutputDeviceMessage.ExecutionId);
        WriteField(stream, TestHostOutputDeviceMessageFieldsId.InstanceId, testHostOutputDeviceMessage.InstanceId);
        WriteField(stream, TestHostOutputDeviceMessageFieldsId.Text, testHostOutputDeviceMessage.Text);
    }

    private static ushort GetFieldCount(TestHostOutputDeviceMessage testHostOutputDeviceMessage) =>
        (ushort)((testHostOutputDeviceMessage.ExecutionId is null ? 0 : 1) +
        (testHostOutputDeviceMessage.InstanceId is null ? 0 : 1) +
        (testHostOutputDeviceMessage.Text is null ? 0 : 1));
}

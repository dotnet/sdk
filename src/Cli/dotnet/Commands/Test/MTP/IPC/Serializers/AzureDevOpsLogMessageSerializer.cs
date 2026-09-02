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

    |---LogText Id---| (2 bytes)
    |---LogText Size---| (4 bytes)
    |---LogText Value---| (n bytes)
*/

internal sealed class AzureDevOpsLogMessageSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => AzureDevOpsLogMessageFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        string? logText = null;

        ushort fieldCount = ReadUShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case AzureDevOpsLogMessageFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                case AzureDevOpsLogMessageFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    break;

                case AzureDevOpsLogMessageFieldsId.LogText:
                    logText = ReadStringValue(stream, fieldSize);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new AzureDevOpsLogMessage(executionId, instanceId, logText);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var message = (AzureDevOpsLogMessage)objectToSerialize;

        WriteUShort(stream, GetFieldCount(message));

        WriteField(stream, AzureDevOpsLogMessageFieldsId.ExecutionId, message.ExecutionId);
        WriteField(stream, AzureDevOpsLogMessageFieldsId.InstanceId, message.InstanceId);
        WriteField(stream, AzureDevOpsLogMessageFieldsId.LogText, message.LogText);
    }

    private static ushort GetFieldCount(AzureDevOpsLogMessage message) =>
        (ushort)((message.ExecutionId is null ? 0 : 1) +
        (message.InstanceId is null ? 0 : 1) +
        (message.LogText is null ? 0 : 1));
}

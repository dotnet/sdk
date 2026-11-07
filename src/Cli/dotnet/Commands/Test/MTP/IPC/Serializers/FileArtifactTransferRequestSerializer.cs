// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal sealed class FileArtifactTransferRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => FileArtifactTransferRequestFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null, instanceId = null, transferId = null, fileName = null;
        long totalLength = -1, offset = -1;
        byte[] data = [];
        bool isFinal = false;
        bool hasExecutionId = false, hasInstanceId = false, hasTransferId = false, hasFileName = false;
        bool hasTotalLength = false, hasOffset = false, hasData = false, hasIsFinal = false;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case FileArtifactTransferRequestFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    hasExecutionId = true;
                    return true;
                case FileArtifactTransferRequestFieldsId.InstanceId:
                    instanceId = ReadStringValue(stream, fieldSize);
                    hasInstanceId = true;
                    return true;
                case FileArtifactTransferRequestFieldsId.TransferId:
                    transferId = ReadStringValue(stream, fieldSize);
                    hasTransferId = true;
                    return true;
                case FileArtifactTransferRequestFieldsId.FileName:
                    fileName = ReadStringValue(stream, fieldSize);
                    hasFileName = true;
                    return true;
                case FileArtifactTransferRequestFieldsId.TotalLength:
                    EnsureFieldSize(fieldSize, sizeof(long));
                    totalLength = ReadLong(stream);
                    hasTotalLength = true;
                    return true;
                case FileArtifactTransferRequestFieldsId.Offset:
                    EnsureFieldSize(fieldSize, sizeof(long));
                    offset = ReadLong(stream);
                    hasOffset = true;
                    return true;
                case FileArtifactTransferRequestFieldsId.Data:
                    if (fieldSize > FileArtifactTransferRequest.MaximumChunkBytes)
                    {
                        throw new InvalidDataException("The file-artifact transfer chunk is too large.");
                    }

                    data = new byte[fieldSize];
                    stream.ReadExactly(data);
                    hasData = true;
                    return true;
                case FileArtifactTransferRequestFieldsId.IsFinal:
                    EnsureFieldSize(fieldSize, sizeof(bool));
                    isFinal = ReadBool(stream);
                    hasIsFinal = true;
                    return true;
                default:
                    return false;
            }
        });

        if (!hasExecutionId || !hasInstanceId || !hasTransferId || !hasFileName ||
            !hasTotalLength || !hasOffset || !hasData || !hasIsFinal || totalLength < 0 || offset < 0)
        {
            throw new InvalidDataException("The file-artifact transfer request is missing or has invalid required fields.");
        }

        return new FileArtifactTransferRequest(executionId, instanceId, transferId, fileName, totalLength, offset, data, isFinal);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var request = (FileArtifactTransferRequest)objectToSerialize;
        WriteUShort(stream, (ushort)(4
            + (request.ExecutionId is null ? 0 : 1)
            + (request.InstanceId is null ? 0 : 1)
            + (request.TransferId is null ? 0 : 1)
            + (request.FileName is null ? 0 : 1)));
        WriteField(stream, FileArtifactTransferRequestFieldsId.ExecutionId, request.ExecutionId);
        WriteField(stream, FileArtifactTransferRequestFieldsId.InstanceId, request.InstanceId);
        WriteField(stream, FileArtifactTransferRequestFieldsId.TransferId, request.TransferId);
        WriteField(stream, FileArtifactTransferRequestFieldsId.FileName, request.FileName);
        WriteField(stream, FileArtifactTransferRequestFieldsId.TotalLength, request.TotalLength);
        WriteField(stream, FileArtifactTransferRequestFieldsId.Offset, request.Offset);
        WriteUShort(stream, FileArtifactTransferRequestFieldsId.Data);
        WriteInt(stream, request.Data.Length);
        stream.Write(request.Data);
        WriteField(stream, FileArtifactTransferRequestFieldsId.IsFinal, request.IsFinal);
    }

    private static void EnsureFieldSize(int actual, int expected)
    {
        if (actual != expected)
        {
            throw new InvalidDataException("The file-artifact transfer field has an invalid size.");
        }
    }
}

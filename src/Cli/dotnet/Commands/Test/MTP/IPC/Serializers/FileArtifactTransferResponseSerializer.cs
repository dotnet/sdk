// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal sealed class FileArtifactTransferResponseSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => FileArtifactTransferResponseFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        long nextOffset = -1;
        string? fullPath = null, error = null;
        ReadFields(stream, (fieldId, fieldSize) =>
        {
            switch (fieldId)
            {
                case FileArtifactTransferResponseFieldsId.NextOffset:
                    if (fieldSize != sizeof(long))
                    {
                        throw new InvalidDataException("The file-artifact transfer offset has an invalid size.");
                    }

                    nextOffset = ReadLong(stream);
                    return true;
                case FileArtifactTransferResponseFieldsId.FullPath:
                    fullPath = ReadStringValue(stream, fieldSize);
                    return true;
                case FileArtifactTransferResponseFieldsId.Error:
                    error = ReadStringValue(stream, fieldSize);
                    return true;
                default:
                    return false;
            }
        });
        if (nextOffset < 0)
        {
            throw new InvalidDataException("The file-artifact transfer response requires a non-negative next offset.");
        }

        return new FileArtifactTransferResponse(nextOffset, fullPath, error);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var response = (FileArtifactTransferResponse)objectToSerialize;
        WriteUShort(stream, (ushort)(1
            + (response.FullPath is null ? 0 : 1)
            + (response.Error is null ? 0 : 1)));
        WriteField(stream, FileArtifactTransferResponseFieldsId.NextOffset, response.NextOffset);
        WriteField(stream, FileArtifactTransferResponseFieldsId.FullPath, response.FullPath);
        WriteField(stream, FileArtifactTransferResponseFieldsId.Error, response.Error);
    }
}

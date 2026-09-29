// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

/*
|---FieldCount---| 2 bytes

|---ExecutionId Id---| (2 bytes)
|---ExecutionId Size---| (4 bytes)
|---ExecutionId Value---| (n bytes)

|---InstanceId---| (2 bytes)
|---InstanceId Size---| (4 bytes)
|---InstanceId Value---| (n bytes)

|---FileArtifactMessageList Id---| (2 bytes)
|---FileArtifactMessageList Size---| (4 bytes)
|---FileArtifactMessageList Value---| (n bytes)
    |---FileArtifactMessageList Length---| (4 bytes)

    |---FileArtifactMessageList[0] FieldCount---| 2 bytes

    |---FileArtifactMessageList[0].FullPath Id---| (2 bytes)
    |---FileArtifactMessageList[0].FullPath Size---| (4 bytes)
    |---FileArtifactMessageList[0].FullPath Value---| (n bytes)

    |---FileArtifactMessageList[0].DisplayName Id---| (2 bytes)
    |---FileArtifactMessageList[0].DisplayName Size---| (4 bytes)
    |---FileArtifactMessageList[0].DisplayName Value---| (n bytes)

    |---FileArtifactMessageList[0].Description Id---| (2 bytes)
    |---FileArtifactMessageList[0].Description Size---| (4 bytes)
    |---FileArtifactMessageList[0].Description Value---| (n bytes)

    |---FileArtifactMessageList[0].TestUid Id---| (2 bytes)
    |---FileArtifactMessageList[0].TestUid Size---| (4 bytes)
    |---FileArtifactMessageList[0].TestUid Value---| (n bytes)

    |---FileArtifactMessageList[0].TestDisplayName Id---| (2 bytes)
    |---FileArtifactMessageList[0].TestDisplayName Size---| (4 bytes)
    |---FileArtifactMessageList[0].TestDisplayName Value---| (n bytes)

    |---FileArtifactMessageList[0].SessionUid Id---| (2 bytes)
    |---FileArtifactMessageList[0].SessionUid Size---| (4 bytes)
    |---FileArtifactMessageList[0].SessionUid Value---| (n bytes)

    |---FileArtifactMessageList[0].Kind Id---| (2 bytes)
    |---FileArtifactMessageList[0].Kind Size---| (4 bytes)
    |---FileArtifactMessageList[0].Kind Value---| (n bytes)

    |---FileArtifactMessageList[0].InputArtifactPaths Id---| (2 bytes)
    |---FileArtifactMessageList[0].InputArtifactPaths Size---| (4 bytes)
    |---FileArtifactMessageList[0].InputArtifactPaths Value---| (n bytes)
        |---InputArtifactPaths Length---| (4 bytes)
        |---InputArtifactPaths[0] Size---| (4 bytes)
        |---InputArtifactPaths[0] Value---| (n bytes)
*/

internal sealed class FileArtifactMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => FileArtifactMessagesFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        List<FileArtifactMessage>? fileArtifactMessages = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            if (fieldId == FileArtifactMessagesFieldsId.FileArtifactMessageList)
            {
                fileArtifactMessages = ReadFieldPayload(
                    stream,
                    fieldSize,
                    ReadFileArtifactMessagesPayload);
                return true;
            }

            return false;
        });

        return new FileArtifactMessages(executionId, instanceId, fileArtifactMessages is null ? [] : [.. fileArtifactMessages]);
    }

    private static List<FileArtifactMessage> ReadFileArtifactMessagesPayload(Stream stream)
    {
        List<FileArtifactMessage> fileArtifactMessages = [];

        int length = ReadCollectionLength(stream, sizeof(ushort));
        for (int i = 0; i < length; i++)
        {
            string? fullPath = null, displayName = null, description = null, testUid = null, testDisplayName = null, sessionUid = null, kind = null;
            string[]? inputArtifactPaths = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case FileArtifactMessageFieldsId.FullPath:
                        fullPath = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.Description:
                        description = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.TestUid:
                        testUid = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.TestDisplayName:
                        testDisplayName = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.SessionUid:
                        sessionUid = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.Kind:
                        kind = ReadStringValue(stream, fieldSize);
                        return true;

                    case FileArtifactMessageFieldsId.InputArtifactPaths:
                        inputArtifactPaths = ReadFieldPayload(
                            stream,
                            fieldSize,
                            ReadInputArtifactPathsPayload);
                        return true;

                    default:
                        return false;
                }
            });

            fileArtifactMessages.Add(new FileArtifactMessage(
                fullPath,
                displayName,
                description,
                testUid,
                testDisplayName,
                sessionUid,
                kind,
                inputArtifactPaths));
        }

        return fileArtifactMessages;
    }

    private static string[] ReadInputArtifactPathsPayload(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(int));
        string[] inputArtifactPaths = new string[length];
        for (int i = 0; i < length; i++)
        {
            inputArtifactPaths[i] = ReadString(stream);
        }

        return inputArtifactPaths;
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var fileArtifactMessages = (FileArtifactMessages)objectToSerialize;

        WriteExecutionScopedHeader(
            stream,
            fileArtifactMessages.ExecutionId,
            fileArtifactMessages.InstanceId,
            (ushort)(IsNullOrEmpty(fileArtifactMessages.FileArtifacts) ? 0 : 1));

        WriteFileArtifactMessagesPayload(stream, fileArtifactMessages.FileArtifacts);
    }

    private static void WriteFileArtifactMessagesPayload(Stream stream, FileArtifactMessage[]? fileArtifactMessageList)
        => WriteListPayload(stream, FileArtifactMessagesFieldsId.FileArtifactMessageList, fileArtifactMessageList, static (s, fileArtifactMessage) =>
        {
            WriteUShort(s, GetFieldCount(fileArtifactMessage));

            WriteField(s, FileArtifactMessageFieldsId.FullPath, fileArtifactMessage.FullPath);
            WriteField(s, FileArtifactMessageFieldsId.DisplayName, fileArtifactMessage.DisplayName);
            WriteField(s, FileArtifactMessageFieldsId.Description, fileArtifactMessage.Description);
            WriteField(s, FileArtifactMessageFieldsId.TestUid, fileArtifactMessage.TestUid);
            WriteField(s, FileArtifactMessageFieldsId.TestDisplayName, fileArtifactMessage.TestDisplayName);
            WriteField(s, FileArtifactMessageFieldsId.SessionUid, fileArtifactMessage.SessionUid);
            WriteField(s, FileArtifactMessageFieldsId.Kind, fileArtifactMessage.Kind);
            WriteInputArtifactPathsPayload(s, fileArtifactMessage.InputArtifactPaths);
        });

    private static void WriteInputArtifactPathsPayload(Stream stream, string[]? inputArtifactPaths)
        => WriteListPayload(
            stream,
            FileArtifactMessageFieldsId.InputArtifactPaths,
            inputArtifactPaths,
            static (s, inputArtifactPath) => WriteString(s, inputArtifactPath));

    private static ushort GetFieldCount(FileArtifactMessage fileArtifactMessage) =>
        (ushort)((fileArtifactMessage.FullPath is null ? 0 : 1) +
        (fileArtifactMessage.DisplayName is null ? 0 : 1) +
        (fileArtifactMessage.Description is null ? 0 : 1) +
        (fileArtifactMessage.TestUid is null ? 0 : 1) +
        (fileArtifactMessage.TestDisplayName is null ? 0 : 1) +
        (fileArtifactMessage.SessionUid is null ? 0 : 1) +
        (fileArtifactMessage.Kind is null ? 0 : 1) +
        (IsNullOrEmpty(fileArtifactMessage.InputArtifactPaths) ? 0 : 1));
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETCOREAPP
#endif

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Test
{
    /*
    |---FieldCount---| 2 bytes

    |---ExecutionId Id---| (2 bytes)
    |---ExecutionId Size---| (4 bytes)
    |---ExecutionId Value---| (n bytes)

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
    */

    internal sealed class FileArtifactMessagesSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => FileArtifactMessagesFieldsId.MessagesSerializerId;

        public object Deserialize(Stream stream)
        {
            string? executionId = null;
            List<FileArtifactMessage>? fileArtifactMessages = null;

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case FileArtifactMessagesFieldsId.ExecutionId:
                        executionId = ReadStringValue(stream, fieldSize);
                        break;

                    case FileArtifactMessagesFieldsId.FileArtifactMessageList:
                        fileArtifactMessages = ReadFileArtifactMessagesPayload(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new FileArtifactMessages(executionId, fileArtifactMessages is null ? [] : [.. fileArtifactMessages]);
        }

        private static List<FileArtifactMessage> ReadFileArtifactMessagesPayload(Stream stream)
        {
            List<FileArtifactMessage> fileArtifactMessages = [];

            int length = ReadInt(stream);
            for (int i = 0; i < length; i++)
            {
                string? fullPath = null, displayName = null, description = null, testUid = null, testDisplayName = null, sessionUid = null;

                int fieldCount = ReadShort(stream);

                for (int j = 0; j < fieldCount; j++)
                {
                    int fieldId = ReadShort(stream);
                    int fieldSize = ReadInt(stream);

                    switch (fieldId)
                    {
                        case FileArtifactMessageFieldsId.FullPath:
                            fullPath = ReadStringValue(stream, fieldSize);
                            break;

                        case FileArtifactMessageFieldsId.DisplayName:
                            displayName = ReadStringValue(stream, fieldSize);
                            break;

                        case FileArtifactMessageFieldsId.Description:
                            description = ReadStringValue(stream, fieldSize);
                            break;

                        case FileArtifactMessageFieldsId.TestUid:
                            testUid = ReadStringValue(stream, fieldSize);
                            break;

                        case FileArtifactMessageFieldsId.TestDisplayName:
                            testDisplayName = ReadStringValue(stream, fieldSize);
                            break;

                        case FileArtifactMessageFieldsId.SessionUid:
                            sessionUid = ReadStringValue(stream, fieldSize);
                            break;

                        default:
                            SetPosition(stream, stream.Position + fieldSize);
                            break;
                    }
                }

                fileArtifactMessages.Add(new FileArtifactMessage(fullPath, displayName, description, testUid, testDisplayName, sessionUid));
            }

            return fileArtifactMessages;
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var fileArtifactMessages = (FileArtifactMessages)objectToSerialize;

            WriteShort(stream, GetFieldCount(fileArtifactMessages));

            WriteField(stream, FileArtifactMessagesFieldsId.ExecutionId, fileArtifactMessages.ExecutionId);
            WriteFileArtifactMessagesPayload(stream, fileArtifactMessages.FileArtifacts);
        }

        private static void WriteFileArtifactMessagesPayload(Stream stream, FileArtifactMessage[]? fileArtifactMessageList)
        {
            if (fileArtifactMessageList is null || fileArtifactMessageList.Length == 0)
            {
                return;
            }

            WriteShort(stream, FileArtifactMessagesFieldsId.FileArtifactMessageList);

            // We will reserve an int (4 bytes)
            // so that we fill the size later, once we write the payload
            WriteInt(stream, 0);

            long before = stream.Position;
            WriteInt(stream, fileArtifactMessageList.Length);
            foreach (FileArtifactMessage fileArtifactMessage in fileArtifactMessageList)
            {
                WriteShort(stream, GetFieldCount(fileArtifactMessage));

                WriteField(stream, FileArtifactMessageFieldsId.FullPath, fileArtifactMessage.FullPath);
                WriteField(stream, FileArtifactMessageFieldsId.DisplayName, fileArtifactMessage.DisplayName);
                WriteField(stream, FileArtifactMessageFieldsId.Description, fileArtifactMessage.Description);
                WriteField(stream, FileArtifactMessageFieldsId.TestUid, fileArtifactMessage.TestUid);
                WriteField(stream, FileArtifactMessageFieldsId.TestDisplayName, fileArtifactMessage.TestDisplayName);
                WriteField(stream, FileArtifactMessageFieldsId.SessionUid, fileArtifactMessage.SessionUid);
            }

            // NOTE: We are able to seek only if we are using a MemoryStream
            // thus, the seek operation is fast as we are only changing the value of a property
            WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
        }

        private static ushort GetFieldCount(FileArtifactMessages fileArtifactMessages) =>
            (ushort)((fileArtifactMessages.ExecutionId is null ? 0 : 1) +
            (IsNullOrEmpty(fileArtifactMessages.FileArtifacts) ? 0 : 1));

        private static ushort GetFieldCount(FileArtifactMessage fileArtifactMessage) =>
            (ushort)((fileArtifactMessage.FullPath is null ? 0 : 1) +
            (fileArtifactMessage.DisplayName is null ? 0 : 1) +
            (fileArtifactMessage.Description is null ? 0 : 1) +
            (fileArtifactMessage.TestUid is null ? 0 : 1) +
            (fileArtifactMessage.TestDisplayName is null ? 0 : 1) +
            (fileArtifactMessage.SessionUid is null ? 0 : 1));
    }
}

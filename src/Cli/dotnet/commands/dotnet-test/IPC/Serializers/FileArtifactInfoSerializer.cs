// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Test
{
    /*
        |---FieldCount---| 2 bytes

        |---File FullPath Id---| 1 (2 bytes)
        |---File FullPath Size---| (4 bytes)
        |---File FullPath Value---| (n bytes)

        |---File DisplayName Id---| 1 (2 bytes)
        |---File DisplayName Size---| (4 bytes)
        |---File DisplayName Value---| (n bytes)

        |---File Description Id---| 1 (2 bytes)
        |---File Description Size---| (4 bytes)
        |---File Description Value---| (n bytes)

        |---File TestUid Id---| 1 (2 bytes)
        |---File TestUid Size---| (4 bytes)
        |---File TestUid Value---| (n bytes)

        |---File TestDisplayName Id---| 1 (2 bytes)
        |---File TestDisplayName Size---| (4 bytes)
        |---File TestDisplayName Value---| (n bytes)

        |---File SessionUid Id---| 1 (2 bytes)
        |---File SessionUid Size---| (4 bytes)
        |---File SessionUid Value---| (n bytes)

        |---File ModulePath Id---| 1 (2 bytes)
        |---File ModulePath Size---| (4 bytes)
        |---File ModulePath Value---| (n bytes)
    */

    internal sealed class FileArtifactInfoSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 7;

        public object Deserialize(Stream stream)
        {
            string? fullPath = null;
            string? displayName = null;
            string? description = null;
            string? testUid = null;
            string? testDisplayName = null;
            string? sessionUid = null;
            string? modulePath = null;

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case FileArtifactInfoFieldsId.FullPath:
                        fullPath = ReadString(stream);
                        break;

                    case FileArtifactInfoFieldsId.DisplayName:
                        displayName = ReadString(stream);
                        break;

                    case FileArtifactInfoFieldsId.Description:
                        description = ReadString(stream);
                        break;

                    case FileArtifactInfoFieldsId.TestUid:
                        testUid = ReadString(stream);
                        break;

                    case FileArtifactInfoFieldsId.TestDisplayName:
                        testDisplayName = ReadString(stream);
                        break;

                    case FileArtifactInfoFieldsId.SessionUid:
                        sessionUid = ReadString(stream);
                        break;

                    case FileArtifactInfoFieldsId.ModulePath:
                        modulePath = ReadString(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new FileArtifactInfo(fullPath, displayName, description, testUid, testDisplayName, sessionUid, modulePath);
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var fileArtifactInfo = (FileArtifactInfo)objectToSerialize;

            WriteShort(stream, GetFieldCount(fileArtifactInfo));

            WriteField(stream, FileArtifactInfoFieldsId.FullPath, fileArtifactInfo.FullPath);
            WriteField(stream, FileArtifactInfoFieldsId.DisplayName, fileArtifactInfo.DisplayName);
            WriteField(stream, FileArtifactInfoFieldsId.Description, fileArtifactInfo.Description);
            WriteField(stream, FileArtifactInfoFieldsId.TestUid, fileArtifactInfo.TestUid);
            WriteField(stream, FileArtifactInfoFieldsId.TestDisplayName, fileArtifactInfo.TestDisplayName);
            WriteField(stream, FileArtifactInfoFieldsId.SessionUid, fileArtifactInfo.SessionUid);
            WriteField(stream, FileArtifactInfoFieldsId.ModulePath, fileArtifactInfo.ModulePath);
        }

        private static ushort GetFieldCount(FileArtifactInfo fileArtifactInfo) =>
            (ushort)((fileArtifactInfo.FullPath is null ? 0 : 1) +
            (fileArtifactInfo.DisplayName is null ? 0 : 1) +
            (fileArtifactInfo.Description is null ? 0 : 1) +
            (fileArtifactInfo.TestUid is null ? 0 : 1) +
            (fileArtifactInfo.TestDisplayName is null ? 0 : 1) +
            (fileArtifactInfo.SessionUid is null ? 0 : 1) +
            (fileArtifactInfo.ModulePath is null ? 0 : 1));
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Test
{
    /*
    |---FieldCount---| 2 bytes

    |---File Uid Id---| (2 bytes)
    |---File Uid Size---| (4 bytes)
    |---File Uid Value---| (n bytes)

    |---File DisplayName Id---| (2 bytes)
    |---File DisplayName Size---| (4 bytes)
    |---File DisplayName Value---| (n bytes)

    |---File ExecutionId Id---| (2 bytes)
    |---File ExecutionId Size---| (4 bytes)
    |---File ExecutionId Value---| (n bytes)
     */

    internal sealed class DiscoveredTestMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => DiscoveredTestMessageFieldsId.MessagesSerializerId;

        public object Deserialize(Stream stream)
        {
            string? uid = null;
            string? displayName = null;
            string? executionId = null;

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case DiscoveredTestMessageFieldsId.Uid:
                        uid = ReadString(stream);
                        break;

                    case DiscoveredTestMessageFieldsId.DisplayName:
                        displayName = ReadString(stream);
                        break;

                    case DiscoveredTestMessageFieldsId.ExecutionId:
                        executionId = ReadString(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new DiscoveredTestMessage(uid, displayName, executionId);
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var discoveredTestMessage = (DiscoveredTestMessage)objectToSerialize;

            WriteShort(stream, GetFieldCount(discoveredTestMessage));

            WriteField(stream, DiscoveredTestMessageFieldsId.Uid, discoveredTestMessage.Uid);
            WriteField(stream, DiscoveredTestMessageFieldsId.DisplayName, discoveredTestMessage.DisplayName);
            WriteField(stream, DiscoveredTestMessageFieldsId.ExecutionId, discoveredTestMessage.ExecutionId);
        }

        private static ushort GetFieldCount(DiscoveredTestMessage discoveredTestMessage) =>
            (ushort)((discoveredTestMessage.Uid is null ? 0 : 1) +
            (discoveredTestMessage.DisplayName is null ? 0 : 1) +
            (discoveredTestMessage.ExecutionId is null ? 0 : 1));
    }
}

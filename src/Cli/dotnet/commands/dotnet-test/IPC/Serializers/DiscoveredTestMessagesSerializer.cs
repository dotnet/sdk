// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETCOREAPP
#nullable enable
#endif

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Test
{
    /*
    |---FieldCount---| 2 bytes

    |---ExecutionId Id---| (2 bytes)
    |---ExecutionId Size---| (4 bytes)
    |---ExecutionId Value---| (n bytes)

    |---DiscoveredTestMessageList Id---| (2 bytes)
    |---DiscoveredTestMessageList Size---| (4 bytes)
    |---DiscoveredTestMessageList Value---| (n bytes)
        |---DiscoveredTestMessageList Length---| (4 bytes)

        |---DiscoveredTestMessageList[0] FieldCount---| 2 bytes

        |---DiscoveredTestMessageList[0].Uid Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].Uid Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].Uid Value---| (n bytes)

        |---DiscoveredTestMessageList[0].DisplayName Id---| (2 bytes)
        |---DiscoveredTestMessageList[0].DisplayName Size---| (4 bytes)
        |---DiscoveredTestMessageList[0].DisplayName Value---| (n bytes)
    */

    internal sealed class DiscoveredTestMessagesSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => DiscoveredTestMessagesFieldsId.MessagesSerializerId;

        public object Deserialize(Stream stream)
        {
            string? executionId = null;
            List<DiscoveredTestMessage>? discoveredTestMessages = null;

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case DiscoveredTestMessagesFieldsId.ExecutionId:
                        executionId = ReadStringValue(stream, fieldSize);
                        break;

                    case DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList:
                        discoveredTestMessages = ReadDiscoveredTestMessagesPayload(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new DiscoveredTestMessages(executionId, discoveredTestMessages is null ? [] : [.. discoveredTestMessages]);
        }

        private static List<DiscoveredTestMessage> ReadDiscoveredTestMessagesPayload(Stream stream)
        {
            List<DiscoveredTestMessage> discoveredTestMessages = [];

            int length = ReadInt(stream);
            for (int i = 0; i < length; i++)
            {
                string? uid = null, displayName = null;

                int fieldCount = ReadShort(stream);

                for (int j = 0; j < fieldCount; j++)
                {
                    int fieldId = ReadShort(stream);
                    int fieldSize = ReadInt(stream);

                    switch (fieldId)
                    {
                        case DiscoveredTestMessageFieldsId.Uid:
                            uid = ReadStringValue(stream, fieldSize);
                            break;

                        case DiscoveredTestMessageFieldsId.DisplayName:
                            displayName = ReadStringValue(stream, fieldSize);
                            break;

                        default:
                            SetPosition(stream, stream.Position + fieldSize);
                            break;
                    }
                }

                discoveredTestMessages.Add(new DiscoveredTestMessage(uid, displayName));
            }

            return discoveredTestMessages;
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var discoveredTestMessages = (DiscoveredTestMessages)objectToSerialize;

            WriteShort(stream, GetFieldCount(discoveredTestMessages));

            WriteField(stream, DiscoveredTestMessagesFieldsId.ExecutionId, discoveredTestMessages.ExecutionId);
            WriteDiscoveredTestMessagesPayload(stream, discoveredTestMessages.DiscoveredMessages);
        }

        private static void WriteDiscoveredTestMessagesPayload(Stream stream, DiscoveredTestMessage[]? discoveredTestMessageList)
        {
            if (discoveredTestMessageList is null || discoveredTestMessageList.Length == 0)
            {
                return;
            }

            WriteShort(stream, DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList);

            // We will reserve an int (4 bytes)
            // so that we fill the size later, once we write the payload
            WriteInt(stream, 0);

            long before = stream.Position;
            WriteInt(stream, discoveredTestMessageList.Length);
            foreach (DiscoveredTestMessage discoveredTestMessage in discoveredTestMessageList)
            {
                WriteShort(stream, GetFieldCount(discoveredTestMessage));

                WriteField(stream, DiscoveredTestMessageFieldsId.Uid, discoveredTestMessage.Uid);
                WriteField(stream, DiscoveredTestMessageFieldsId.DisplayName, discoveredTestMessage.DisplayName);
            }

            // NOTE: We are able to seek only if we are using a MemoryStream
            // thus, the seek operation is fast as we are only changing the value of a property
            WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
        }

        private static ushort GetFieldCount(DiscoveredTestMessages discoveredTestMessages) =>
            (ushort)((discoveredTestMessages.ExecutionId is null ? 0 : 1) +
            (IsNullOrEmpty(discoveredTestMessages.DiscoveredMessages) ? 0 : 1));

        private static ushort GetFieldCount(DiscoveredTestMessage discoveredTestMessage) =>
            (ushort)((discoveredTestMessage.Uid is null ? 0 : 1) +
            (discoveredTestMessage.DisplayName is null ? 0 : 1));
    }
}

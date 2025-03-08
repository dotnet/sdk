// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Test
{
    internal sealed class HandshakeMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => HandshakeMessageFieldsId.MessagesSerializerId;

        public object Deserialize(Stream stream)
        {
            Dictionary<byte, string> properties = new();

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
            {
                properties.Add(ReadByte(stream), ReadString(stream));
            }

            return new HandshakeMessage(properties);
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var handshakeMessage = (HandshakeMessage)objectToSerialize;

            if (handshakeMessage.Properties is null || handshakeMessage.Properties.Count == 0)
            {
                return;
            }

            WriteShort(stream, (ushort)handshakeMessage.Properties.Count);
            foreach (KeyValuePair<byte, string> property in handshakeMessage.Properties)
            {
                WriteField(stream, property.Key);
                WriteField(stream, property.Value);
            }
        }
    }
}

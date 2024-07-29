// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Test
{
    /*
        |---FieldCount---| 2 bytes

        |---Type Id---| 1 (2 bytes)
        |---Type Size---| (4 bytes)
        |---Type Value---| (n bytes)

        |---SessionUid Id---| 1 (2 bytes)
        |---SessionUid Size---| (4 bytes)
        |---SessionUid Value---| (n bytes)

        |---ModulePath Id---| 1 (2 bytes)
        |---ModulePath Size---| (4 bytes)
        |---ModulePath Value---| (n bytes)
    */

    internal sealed class TestSessionEventSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 8;

        public object Deserialize(Stream stream)
        {
            string type = string.Empty;
            string sessionUid = string.Empty;
            string modulePath = string.Empty;

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case TestSessionEventFieldsId.SessionType:
                        type = ReadString(stream);
                        break;

                    case TestSessionEventFieldsId.SessionUid:
                        sessionUid = ReadString(stream);
                        break;

                    case TestSessionEventFieldsId.ModulePath:
                        modulePath = ReadString(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new TestSessionEvent(type, sessionUid, modulePath);
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var sessionEvent = (TestSessionEvent)objectToSerialize;

            WriteShort(stream, GetFieldCount(sessionEvent));

            WriteField(stream, TestSessionEventFieldsId.SessionType, sessionEvent.SessionType);
            WriteField(stream, TestSessionEventFieldsId.SessionUid, sessionEvent.SessionUid);
            WriteField(stream, TestSessionEventFieldsId.ModulePath, sessionEvent.ModulePath);
        }

        private static ushort GetFieldCount(TestSessionEvent sessionEvent) =>
            (ushort)((IsNull(sessionEvent.SessionType) ? 0 : 1) +
            (IsNull(sessionEvent.SessionUid) ? 0 : 1) +
            (IsNull(sessionEvent.ModulePath) ? 0 : 1));
    }
}

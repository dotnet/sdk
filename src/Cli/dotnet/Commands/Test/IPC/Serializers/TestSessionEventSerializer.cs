﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

/*
    |---FieldCount---| 2 bytes

    |---Type Id---| (2 bytes)
    |---Type Size---| (4 bytes)
    |---Type Value---| (n bytes)

    |---SessionUid Id---| (2 bytes)
    |---SessionUid Size---| (4 bytes)
    |---SessionUid Value---| (n bytes)

    |---ExecutionId Id---| (2 bytes)
    |---ExecutionId Size---| (4 bytes)
    |---ExecutionId Value---| (n bytes)
*/

internal sealed class TestSessionEventSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => TestSessionEventFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        byte? type = null;
        string? sessionUid = null;
        string? executionId = null;

        ushort fieldCount = ReadShort(stream);

        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case TestSessionEventFieldsId.SessionType:
                    type = ReadByte(stream);
                    break;

                case TestSessionEventFieldsId.SessionUid:
                    sessionUid = ReadStringValue(stream, fieldSize);
                    break;

                case TestSessionEventFieldsId.ExecutionId:
                    executionId = ReadStringValue(stream, fieldSize);
                    break;

                default:
                    // If we don't recognize the field id, skip the payload corresponding to that field
                    SetPosition(stream, stream.Position + fieldSize);
                    break;
            }
        }

        return new TestSessionEvent(type, sessionUid, executionId);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

        var testSessionEvent = (TestSessionEvent)objectToSerialize;

        WriteShort(stream, GetFieldCount(testSessionEvent));

        WriteField(stream, TestSessionEventFieldsId.SessionType, testSessionEvent.SessionType);
        WriteField(stream, TestSessionEventFieldsId.SessionUid, testSessionEvent.SessionUid);
        WriteField(stream, TestSessionEventFieldsId.ExecutionId, testSessionEvent.ExecutionId);
    }

    private static ushort GetFieldCount(TestSessionEvent testSessionEvent) =>
        (ushort)((testSessionEvent.SessionType is null ? 0 : 1) +
        (testSessionEvent.SessionUid is null ? 0 : 1) +
        (testSessionEvent.ExecutionId is null ? 0 : 1));
}

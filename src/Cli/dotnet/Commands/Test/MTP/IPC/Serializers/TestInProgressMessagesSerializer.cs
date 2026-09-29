// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

/*
|---FieldCount---| 2 bytes

|---ExecutionId Id---| (2 bytes)
|---ExecutionId Size---| (4 bytes)
|---ExecutionId Value---| (n bytes)

|---InstanceId Id---| (2 bytes)
|---InstanceId Size---| (4 bytes)
|---InstanceId Value---| (n bytes)

|---TestInProgressMessageList Id---| (2 bytes)
|---TestInProgressMessageList Size---| (4 bytes)
|---TestInProgressMessageList Value---| (n bytes)
    |---TestInProgressMessageList Length---| (4 bytes)

    |---TestInProgressMessageList[0] FieldCount---| 2 bytes

    |---TestInProgressMessageList[0].Uid Id---| (2 bytes)
    |---TestInProgressMessageList[0].Uid Size---| (4 bytes)
    |---TestInProgressMessageList[0].Uid Value---| (n bytes)

    |---TestInProgressMessageList[0].DisplayName Id---| (2 bytes)
    |---TestInProgressMessageList[0].DisplayName Size---| (4 bytes)
    |---TestInProgressMessageList[0].DisplayName Value---| (n bytes)
*/

internal sealed class TestInProgressMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => TestInProgressMessagesFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        List<TestInProgressMessage>? inProgressMessages = null;

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            if (fieldId == TestInProgressMessagesFieldsId.TestInProgressMessageList)
            {
                inProgressMessages = ReadInProgressMessagesPayload(stream);
                return true;
            }

            return false;
        });

        return new TestInProgressMessages(executionId, instanceId, inProgressMessages is null ? [] : [.. inProgressMessages]);
    }

    private static List<TestInProgressMessage> ReadInProgressMessagesPayload(Stream stream)
    {
        List<TestInProgressMessage> inProgressMessages = [];

        int length = ReadInt(stream);
        for (int i = 0; i < length; i++)
        {
            string? uid = null, displayName = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case TestInProgressMessageFieldsId.Uid:
                        uid = ReadStringValue(stream, fieldSize);
                        return true;

                    case TestInProgressMessageFieldsId.DisplayName:
                        displayName = ReadStringValue(stream, fieldSize);
                        return true;

                    default:
                        return false;
                }
            });

            inProgressMessages.Add(new TestInProgressMessage(uid, displayName));
        }

        return inProgressMessages;
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var inProgressMessages = (TestInProgressMessages)objectToSerialize;

        WriteExecutionScopedHeader(
            stream,
            inProgressMessages.ExecutionId,
            inProgressMessages.InstanceId,
            (ushort)(IsNullOrEmpty(inProgressMessages.InProgressMessages) ? 0 : 1));

        WriteInProgressMessagesPayload(stream, inProgressMessages.InProgressMessages);
    }

    private static void WriteInProgressMessagesPayload(Stream stream, TestInProgressMessage[]? inProgressMessageList)
        => WriteListPayload(stream, TestInProgressMessagesFieldsId.TestInProgressMessageList, inProgressMessageList, static (s, inProgressMessage) =>
        {
            WriteUShort(s, GetFieldCount(inProgressMessage));

            WriteField(s, TestInProgressMessageFieldsId.Uid, inProgressMessage.Uid);
            WriteField(s, TestInProgressMessageFieldsId.DisplayName, inProgressMessage.DisplayName);
        });

    private static ushort GetFieldCount(TestInProgressMessage inProgressMessage) =>
        (ushort)((inProgressMessage.Uid is null ? 0 : 1) +
        (inProgressMessage.DisplayName is null ? 0 : 1));
}

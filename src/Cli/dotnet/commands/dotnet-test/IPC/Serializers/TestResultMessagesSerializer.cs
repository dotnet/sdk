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

    |---SuccessfulTestMessageList Id---| (2 bytes)
    |---SuccessfulTestMessageList Size---| (4 bytes)
    |---SuccessfulTestMessageList Value---| (n bytes)
        |---SuccessfulTestMessageList Length---| (4 bytes)

        |---SuccessfulTestMessageList[0] FieldCount---| 2 bytes

        |---SuccessfulTestMessageList[0].Uid Id---| (2 bytes)
        |---SuccessfulTestMessageList[0].Uid Size---| (4 bytes)
        |---SuccessfulTestMessageList[0].Uid Value---| (n bytes)

        |---SuccessfulTestMessageList[0].DisplayName Id---| (2 bytes)
        |---SuccessfulTestMessageList[0].DisplayName Size---| (4 bytes)
        |---SuccessfulTestMessageList[0].DisplayName Value---| (n bytes)

        |---SuccessfulTestMessageList[0].State Id---| (2 bytes)
        |---SuccessfulTestMessageList[0].State Size---| (4 bytes)
        |---SuccessfulTestMessageList[0].State Value---| (n bytes)

        |---SuccessfulTestMessageList[0].Reason Id---| (2 bytes)
        |---SuccessfulTestMessageList[0].Reason Size---| (4 bytes)
        |---SuccessfulTestMessageList[0].Reason Value---| (n bytes)

        |---SuccessfulTestMessageList[0].SessionUid Id---| (2 bytes)
        |---SuccessfulTestMessageList[0].SessionUid Size---| (4 bytes)
        |---SuccessfulTestMessageList[0].SessionUid Value---| (n bytes)

    |---FailedTestMessageList Id---| (2 bytes)
    |---FailedTestMessageList Size---| (4 bytes)
    |---FailedTestMessageList Value---| (n bytes)
        |---FailedTestMessageList Length---| (4 bytes)

        |---FailedTestMessageList[0] FieldCount---| 2 bytes

        |---FailedTestMessageList[0].Uid Id---| (2 bytes)
        |---FailedTestMessageList[0].Uid Size---| (4 bytes)
        |---FailedTestMessageList[0].Uid Value---| (n bytes)

        |---FailedTestMessageList[0].DisplayName Id---| (2 bytes)
        |---FailedTestMessageList[0].DisplayName Size---| (4 bytes)
        |---FailedTestMessageList[0].DisplayName Value---| (n bytes)

        |---FailedTestMessageList[0].State Id---| (2 bytes)
        |---FailedTestMessageList[0].State Size---| (4 bytes)
        |---FailedTestMessageList[0].State Value---| (n bytes)

        |---FailedTestMessageList[0].Reason Id---| (2 bytes)
        |---FailedTestMessageList[0].Reason Size---| (4 bytes)
        |---FailedTestMessageList[0].Reason Value---| (n bytes)

        |---FailedTestMessageList[0].ErrorMessage Id---| (2 bytes)
        |---FailedTestMessageList[0].ErrorMessage Size---| (4 bytes)
        |---FailedTestMessageList[0].ErrorMessage Value---| (n bytes)

        |---FailedTestMessageList[0].ErrorStackTrace Id---| (2 bytes)
        |---FailedTestMessageList[0].ErrorStackTrace Size---| (4 bytes)
        |---FailedTestMessageList[0].ErrorStackTrace Value---| (n bytes)

        |---FailedTestMessageList[0].SessionUid Id---| (2 bytes)
        |---FailedTestMessageList[0].SessionUid Size---| (4 bytes)
        |---FailedTestMessageList[0].SessionUid Value---| (n bytes)
    */

    internal sealed class TestResultMessagesSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => TestResultMessagesFieldsId.MessagesSerializerId;

        public object Deserialize(Stream stream)
        {
            string? executionId = null;
            List<SuccessfulTestResultMessage>? successfulTestResultMessages = null;
            List<FailedTestResultMessage>? failedTestResultMessages = null;

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
            {
                int fieldId = ReadShort(stream);
                int fieldSize = ReadInt(stream);

                switch (fieldId)
                {
                    case TestResultMessagesFieldsId.ExecutionId:
                        executionId = ReadString(stream);
                        break;

                    case TestResultMessagesFieldsId.SuccessfulTestMessageList:
                        successfulTestResultMessages = ReadSuccessfulTestMessagesPayload(stream);
                        break;

                    case TestResultMessagesFieldsId.FailedTestMessageList:
                        failedTestResultMessages = ReadFailedTestMessagesPayload(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new TestResultMessages(
                executionId,
                successfulTestResultMessages is null ? [] : [.. successfulTestResultMessages],
                failedTestResultMessages is null ? [] : [.. failedTestResultMessages]);
        }

        private static List<SuccessfulTestResultMessage> ReadSuccessfulTestMessagesPayload(Stream stream)
        {
            List<SuccessfulTestResultMessage> successfulTestResultMessages = [];

            int length = ReadInt(stream);
            for (int i = 0; i < length; i++)
            {
                string? uid = null, displayName = null, reason = null, sessionUid = null;
                byte? state = null;

                int fieldCount = ReadShort(stream);

                for (int j = 0; j < fieldCount; j++)
                {
                    int fieldId = ReadShort(stream);
                    int fieldSize = ReadInt(stream);

                    switch (fieldId)
                    {
                        case SuccessfulTestResultMessageFieldsId.Uid:
                            uid = ReadString(stream);
                            break;

                        case SuccessfulTestResultMessageFieldsId.DisplayName:
                            displayName = ReadString(stream);
                            break;

                        case SuccessfulTestResultMessageFieldsId.State:
                            state = ReadByte(stream);
                            break;

                        case SuccessfulTestResultMessageFieldsId.Reason:
                            reason = ReadString(stream);
                            break;

                        case SuccessfulTestResultMessageFieldsId.SessionUid:
                            sessionUid = ReadString(stream);
                            break;

                        default:
                            SetPosition(stream, stream.Position + fieldSize);
                            break;
                    }
                }

                successfulTestResultMessages.Add(new SuccessfulTestResultMessage(uid, displayName, state, reason, sessionUid));
            }

            return successfulTestResultMessages;
        }

        private static List<FailedTestResultMessage> ReadFailedTestMessagesPayload(Stream stream)
        {
            List<FailedTestResultMessage> failedTestResultMessages = [];

            int length = ReadInt(stream);
            for (int i = 0; i < length; i++)
            {
                string? uid = null, displayName = null, reason = null, sessionUid = null, errorMessage = null, errorStackTrace = null;
                byte? state = null;

                int fieldCount = ReadShort(stream);

                for (int j = 0; j < fieldCount; j++)
                {
                    int fieldId = ReadShort(stream);
                    int fieldSize = ReadInt(stream);

                    switch (fieldId)
                    {
                        case FailedTestResultMessageFieldsId.Uid:
                            uid = ReadString(stream);
                            break;

                        case FailedTestResultMessageFieldsId.DisplayName:
                            displayName = ReadString(stream);
                            break;

                        case FailedTestResultMessageFieldsId.State:
                            state = ReadByte(stream);
                            break;

                        case FailedTestResultMessageFieldsId.Reason:
                            reason = ReadString(stream);
                            break;

                        case FailedTestResultMessageFieldsId.ErrorMessage:
                            errorMessage = ReadString(stream);
                            break;

                        case FailedTestResultMessageFieldsId.ErrorStackTrace:
                            errorStackTrace = ReadString(stream);
                            break;

                        case FailedTestResultMessageFieldsId.SessionUid:
                            sessionUid = ReadString(stream);
                            break;

                        default:
                            SetPosition(stream, stream.Position + fieldSize);
                            break;
                    }
                }

                failedTestResultMessages.Add(new FailedTestResultMessage(uid, displayName, state, reason, errorMessage, errorStackTrace, sessionUid));
            }

            return failedTestResultMessages;
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var testResultMessages = (TestResultMessages)objectToSerialize;

            WriteShort(stream, GetFieldCount(testResultMessages));

            WriteField(stream, TestResultMessagesFieldsId.ExecutionId, testResultMessages.ExecutionId);
            WriteSuccessfulTestMessagesPayload(stream, testResultMessages.SuccessfulTestMessages);
            WriteFailedTestMessagesPayload(stream, testResultMessages.FailedTestMessages);
        }

        private static void WriteSuccessfulTestMessagesPayload(Stream stream, SuccessfulTestResultMessage[]? successfulTestResultMessages)
        {
            if (successfulTestResultMessages is null || successfulTestResultMessages.Length == 0)
            {
                return;
            }

            WriteShort(stream, TestResultMessagesFieldsId.SuccessfulTestMessageList);

            // We will reserve an int (4 bytes)
            // so that we fill the size later, once we write the payload
            WriteInt(stream, 0);

            long before = stream.Position;
            WriteInt(stream, successfulTestResultMessages.Length);
            foreach (SuccessfulTestResultMessage successfulTestResultMessage in successfulTestResultMessages)
            {
                WriteShort(stream, GetFieldCount(successfulTestResultMessage));

                WriteField(stream, SuccessfulTestResultMessageFieldsId.Uid, successfulTestResultMessage.Uid);
                WriteField(stream, SuccessfulTestResultMessageFieldsId.DisplayName, successfulTestResultMessage.DisplayName);
                WriteField(stream, SuccessfulTestResultMessageFieldsId.State, successfulTestResultMessage.State);
                WriteField(stream, SuccessfulTestResultMessageFieldsId.Reason, successfulTestResultMessage.Reason);
                WriteField(stream, SuccessfulTestResultMessageFieldsId.SessionUid, successfulTestResultMessage.SessionUid);
            }

            // NOTE: We are able to seek only if we are using a MemoryStream
            // thus, the seek operation is fast as we are only changing the value of a property
            WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
        }

        private static void WriteFailedTestMessagesPayload(Stream stream, FailedTestResultMessage[]? failedTestResultMessages)
        {
            if (failedTestResultMessages is null || failedTestResultMessages.Length == 0)
            {
                return;
            }

            WriteShort(stream, TestResultMessagesFieldsId.FailedTestMessageList);

            // We will reserve an int (4 bytes)
            // so that we fill the size later, once we write the payload
            WriteInt(stream, 0);

            long before = stream.Position;
            WriteInt(stream, failedTestResultMessages.Length);
            foreach (FailedTestResultMessage failedTestResultMessage in failedTestResultMessages)
            {
                WriteShort(stream, GetFieldCount(failedTestResultMessage));

                WriteField(stream, FailedTestResultMessageFieldsId.Uid, failedTestResultMessage.Uid);
                WriteField(stream, FailedTestResultMessageFieldsId.DisplayName, failedTestResultMessage.DisplayName);
                WriteField(stream, FailedTestResultMessageFieldsId.State, failedTestResultMessage.State);
                WriteField(stream, FailedTestResultMessageFieldsId.Reason, failedTestResultMessage.Reason);
                WriteField(stream, FailedTestResultMessageFieldsId.ErrorMessage, failedTestResultMessage.ErrorMessage);
                WriteField(stream, FailedTestResultMessageFieldsId.ErrorStackTrace, failedTestResultMessage.ErrorStackTrace);
                WriteField(stream, FailedTestResultMessageFieldsId.SessionUid, failedTestResultMessage.SessionUid);
            }

            // NOTE: We are able to seek only if we are using a MemoryStream
            // thus, the seek operation is fast as we are only changing the value of a property
            WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
        }

        private static ushort GetFieldCount(TestResultMessages testResultMessages) =>
            (ushort)((testResultMessages.ExecutionId is null ? 0 : 1) +
            (IsNullOrEmpty(testResultMessages.SuccessfulTestMessages) ? 0 : 1) +
            (IsNullOrEmpty(testResultMessages.FailedTestMessages) ? 0 : 1));

        private static ushort GetFieldCount(SuccessfulTestResultMessage successfulTestResultMessage) =>
            (ushort)((successfulTestResultMessage.Uid is null ? 0 : 1) +
            (successfulTestResultMessage.DisplayName is null ? 0 : 1) +
            (successfulTestResultMessage.State is null ? 0 : 1) +
            (successfulTestResultMessage.Reason is null ? 0 : 1) +
            (successfulTestResultMessage.SessionUid is null ? 0 : 1));

        private static ushort GetFieldCount(FailedTestResultMessage failedTestResultMessage) =>
            (ushort)((failedTestResultMessage.Uid is null ? 0 : 1) +
            (failedTestResultMessage.DisplayName is null ? 0 : 1) +
            (failedTestResultMessage.State is null ? 0 : 1) +
            (failedTestResultMessage.Reason is null ? 0 : 1) +
            (failedTestResultMessage.ErrorMessage is null ? 0 : 1) +
            (failedTestResultMessage.ErrorStackTrace is null ? 0 : 1) +
            (failedTestResultMessage.SessionUid is null ? 0 : 1));
    }
}

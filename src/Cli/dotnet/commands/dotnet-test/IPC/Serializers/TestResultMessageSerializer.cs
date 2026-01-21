// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Test
{
    /*
    |---FieldCount---| 2 bytes

    |---Test Uid Id---| 1 (2 bytes)
    |---Test Uid Size---| (4 bytes)
    |---Test Uid Value---| (n bytes)

    |---Test DisplayName Id---| 1 (2 bytes)
    |---Test DisplayName Size---| (4 bytes)
    |---Test DisplayName Value---| (n bytes)

    |---Test State Id---| 1 (2 bytes)
    |---Test State Size---| (4 bytes)
    |---Test State Value---| (n bytes)

    |---Test Reason Id---| 1 (2 bytes)
    |---Test Reason Size---| (4 bytes)
    |---Test Reason Value---| (n bytes)

    |---Test SessionUid Id---| 1 (2 bytes)
    |---Test SessionUid Size---| (4 bytes)
    |---Test SessionUid Value---| (n bytes)

    |---Test ModulePath Id---| 1 (2 bytes)
    |---Test ModulePath Size---| (4 bytes)
    |---Test ModulePath Value---| (n bytes)
    */

    internal sealed class SuccessfulTestResultMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 5;

        public object Deserialize(Stream stream)
        {
            string? uid = null;
            string? displayName = null;
            string? state = null;
            string? reason = null;
            string? sessionUid = null;
            string? modulePath = null;

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
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
                        state = ReadString(stream);
                        break;

                    case SuccessfulTestResultMessageFieldsId.Reason:
                        reason = ReadString(stream);
                        break;

                    case SuccessfulTestResultMessageFieldsId.SessionUid:
                        sessionUid = ReadString(stream);
                        break;

                    case SuccessfulTestResultMessageFieldsId.ModulePath:
                        modulePath = ReadString(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new SuccessfulTestResultMessage(uid, displayName, state, reason, sessionUid, modulePath);
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var testResultMessage = (SuccessfulTestResultMessage)objectToSerialize;

            WriteShort(stream, GetFieldCount(testResultMessage));

            WriteField(stream, SuccessfulTestResultMessageFieldsId.Uid, testResultMessage.Uid);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.DisplayName, testResultMessage.DisplayName);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.State, testResultMessage.State);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.Reason, testResultMessage.Reason);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.SessionUid, testResultMessage.SessionUid);
            WriteField(stream, SuccessfulTestResultMessageFieldsId.ModulePath, testResultMessage.ModulePath);
        }

        private static ushort GetFieldCount(SuccessfulTestResultMessage testResultMessage) =>
            (ushort)((testResultMessage.Uid is null ? 0 : 1) +
            (testResultMessage.DisplayName is null ? 0 : 1) +
            (testResultMessage.State is null ? 0 : 1) +
            (testResultMessage.Reason is null ? 0 : 1) +
            (testResultMessage.SessionUid is null ? 0 : 1) +
            (testResultMessage.ModulePath is null ? 0 : 1));
    }

    /*
    |---FieldCount---| 2 bytes

    |---Test Uid Id---| 1 (2 bytes)
    |---Test Uid Size---| (4 bytes)
    |---Test Uid Value---| (n bytes)

    |---Test DisplayName Id---| 1 (2 bytes)
    |---Test DisplayName Size---| (4 bytes)
    |---Test DisplayName Value---| (n bytes)

    |---Test State Id---| 1 (2 bytes)
    |---Test State Size---| (4 bytes)
    |---Test State Value---| (n bytes)

    |---Test Reason Id---| 1 (2 bytes)
    |---Test Reason Size---| (4 bytes)
    |---Test Reason Value---| (n bytes)

    |---Test ErrorMessage Id---| 1 (2 bytes)
    |---Test ErrorMessage Size---| (4 bytes)
    |---Test ErrorMessage Value---| (n bytes)

    |---Test ErrorStackTrace Id---| 1 (2 bytes)
    |---Test ErrorStackTrace Size---| (4 bytes)
    |---Test ErrorStackTrace Value---| (n bytes)

    |---Test SessionUid Id---| 1 (2 bytes)
    |---Test SessionUid Size---| (4 bytes)
    |---Test SessionUid Value---| (n bytes)

    |---Test ModulePath Id---| 1 (2 bytes)
    |---Test ModulePath Size---| (4 bytes)
    |---Test ModulePath Value---| (n bytes)
    */

    internal sealed class FailedTestResultMessageSerializer : BaseSerializer, INamedPipeSerializer
    {
        public int Id => 6;

        public object Deserialize(Stream stream)
        {
            string? uid = null;
            string? displayName = null;
            string? state = null;
            string? reason = null;
            string? errorMessage = null;
            string? errorStackTrace = null;
            string? sessionUid = null;
            string? modulePath = null;

            ushort fieldCount = ReadShort(stream);

            for (int i = 0; i < fieldCount; i++)
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
                        state = ReadString(stream);
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

                    case FailedTestResultMessageFieldsId.ModulePath:
                        modulePath = ReadString(stream);
                        break;

                    default:
                        // If we don't recognize the field id, skip the payload corresponding to that field
                        SetPosition(stream, stream.Position + fieldSize);
                        break;
                }
            }

            return new FailedTestResultMessage(uid, displayName, state, reason, errorMessage, errorStackTrace, sessionUid, modulePath);
        }

        public void Serialize(object objectToSerialize, Stream stream)
        {
            Debug.Assert(stream.CanSeek, "We expect a seekable stream.");

            var testResultMessage = (FailedTestResultMessage)objectToSerialize;

            WriteShort(stream, GetFieldCount(testResultMessage));

            WriteField(stream, FailedTestResultMessageFieldsId.Uid, testResultMessage.Uid);
            WriteField(stream, FailedTestResultMessageFieldsId.DisplayName, testResultMessage.DisplayName);
            WriteField(stream, FailedTestResultMessageFieldsId.State, testResultMessage.State);
            WriteField(stream, FailedTestResultMessageFieldsId.Reason, testResultMessage.Reason);
            WriteField(stream, FailedTestResultMessageFieldsId.ErrorMessage, testResultMessage.ErrorMessage);
            WriteField(stream, FailedTestResultMessageFieldsId.ErrorStackTrace, testResultMessage.ErrorStackTrace);
            WriteField(stream, FailedTestResultMessageFieldsId.SessionUid, testResultMessage.SessionUid);
            WriteField(stream, FailedTestResultMessageFieldsId.ModulePath, testResultMessage.ModulePath);
        }

        private static ushort GetFieldCount(FailedTestResultMessage testResultMessage) =>
            (ushort)((testResultMessage.Uid is null ? 0 : 1) +
            (testResultMessage.DisplayName is null ? 0 : 1) +
            (testResultMessage.State is null ? 0 : 1) +
            (testResultMessage.Reason is null ? 0 : 1) +
            (testResultMessage.ErrorMessage is null ? 0 : 1) +
            (testResultMessage.ErrorStackTrace is null ? 0 : 1) +
            (testResultMessage.SessionUid is null ? 0 : 1) +
            (testResultMessage.ModulePath is null ? 0 : 1));
    }
}

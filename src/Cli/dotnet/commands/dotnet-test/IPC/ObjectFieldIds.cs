// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// WARNING: Please note this file needs to be kept aligned with the one in the testfx repo.
// The protocol follows the concept of optional properties.
// The id is used to identify the property in the stream and it will be skipped if it's not recognized.
// We can add new properties with new ids, but we CANNOT change the existing ids (to support backwards compatibility).
namespace Microsoft.DotNet.Tools.Test
{
    internal static class CommandLineOptionMessagesFieldsId
    {
        internal const ushort ModulePath = 1;
        internal const ushort CommandLineOptionMessageList = 2;
    }

    internal static class CommandLineOptionMessageFieldsId
    {
        internal const ushort Name = 1;
        internal const ushort Description = 2;
        internal const ushort IsHidden = 3;
        internal const ushort IsBuiltIn = 4;
    }

    internal static class DiscoveredTestMessageFieldsId
    {
        internal const ushort Uid = 1;
        internal const ushort DisplayName = 2;
        internal const ushort ExecutionId = 3;
    }

    internal static class SuccessfulTestResultMessageFieldsId
    {
        internal const ushort Uid = 1;
        internal const ushort DisplayName = 2;
        internal const ushort State = 3;
        internal const ushort Reason = 4;
        internal const ushort SessionUid = 5;
        internal const ushort ExecutionId = 6;
    }

    internal static class FailedTestResultMessageFieldsId
    {
        internal const ushort Uid = 1;
        internal const ushort DisplayName = 2;
        internal const ushort State = 3;
        internal const ushort Reason = 4;
        internal const ushort ErrorMessage = 5;
        internal const ushort ErrorStackTrace = 6;
        internal const ushort SessionUid = 7;
        internal const ushort ExecutionId = 8;
    }

    internal static class FileArtifactInfoFieldsId
    {
        internal const ushort FullPath = 1;
        internal const ushort DisplayName = 2;
        internal const ushort Description = 3;
        internal const ushort TestUid = 4;
        internal const ushort TestDisplayName = 5;
        internal const ushort SessionUid = 6;
        internal const ushort ExecutionId = 7;
    }

    internal static class TestSessionEventFieldsId
    {
        internal const ushort SessionType = 1;
        internal const ushort SessionUid = 2;
        internal const ushort ExecutionId = 3;
    }

    internal static class SerializerIds
    {
        internal const int VoidResponseSerializerId = 0;
        internal const int TestHostProcessExitRequestSerializerId = 1;
        internal const int TestHostProcessPIDRequestSerializerId = 2;
        internal const int CommandLineOptionMessagesSerializer = 3;
        internal const int ModuleSerializerId = 4;
        internal const int SuccessfulTestResultMessageSerializerId = 5;
        internal const int FailedTestResultMessageSerializerId = 6;
        internal const int FileArtifactInfoSerializerId = 7;
        internal const int TestSessionEventSerializerId = 8;
        internal const int HandshakeInfoSerializerId = 9;
        internal const int DiscoveredTestMessageSerializerId = 10;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// WARNING: Please note this file needs to be kept aligned with the one in the testfx repo.
// The protocol follows the concept of optional properties.
// The id is used to identify the property in the stream and it will be skipped if it's not recognized.
// We can add new properties with new ids, but we CANNOT change the existing ids (to support backwards compatibility).
namespace Microsoft.DotNet.Tools.Test
{
    internal static class VoidResponseFieldsId
    {
        public const int MessagesSerializerId = 0;
    }

    internal static class TestHostProcessExitRequestFieldsId
    {
        public const int MessagesSerializerId = 1;
    }

    internal static class TestHostProcessPIDRequestFieldsId
    {
        public const int MessagesSerializerId = 2;
    }

    internal static class CommandLineOptionMessagesFieldsId
    {
        public const int MessagesSerializerId = 3;

        public const ushort ModulePath = 1;
        public const ushort CommandLineOptionMessageList = 2;
    }

    internal static class CommandLineOptionMessageFieldsId
    {
        public const ushort Name = 1;
        public const ushort Description = 2;
        public const ushort IsHidden = 3;
        public const ushort IsBuiltIn = 4;
    }

    internal static class ModuleFieldsId
    {
        public const int MessagesSerializerId = 4;
    }

    internal static class DiscoveredTestMessagesFieldsId
    {
        public const int MessagesSerializerId = 5;

        public const ushort ExecutionId = 1;
        public const ushort DiscoveredTestMessageList = 2;
    }

    internal static class DiscoveredTestMessageFieldsId
    {
        public const ushort Uid = 1;
        public const ushort DisplayName = 2;
    }

    internal static class TestResultMessagesFieldsId
    {
        public const int MessagesSerializerId = 6;

        public const ushort ExecutionId = 1;
        public const ushort SuccessfulTestMessageList = 2;
        public const ushort FailedTestMessageList = 3;
    }

    internal static class SuccessfulTestResultMessageFieldsId
    {
        public const ushort Uid = 1;
        public const ushort DisplayName = 2;
        public const ushort State = 3;
        public const ushort Duration = 4;
        public const ushort Reason = 5;
        public const ushort StandardOutput = 6;
        public const ushort ErrorOutput = 7;
        public const ushort SessionUid = 8;
    }

    internal static class FailedTestResultMessageFieldsId
    {
        public const ushort Uid = 1;
        public const ushort DisplayName = 2;
        public const ushort State = 3;
        public const ushort Duration = 4;
        public const ushort Reason = 5;
        public const ushort ExceptionMessageList = 6;
        public const ushort StandardOutput = 7;
        public const ushort ErrorOutput = 8;
        public const ushort SessionUid = 9;
    }

    internal static class ExceptionMessageFieldsId
    {
        public const ushort ErrorMessage = 1;
        public const ushort ErrorType = 2;
        public const ushort StackTrace = 3;
    }

    internal static class FileArtifactMessagesFieldsId
    {
        public const int MessagesSerializerId = 7;

        public const ushort ExecutionId = 1;
        public const ushort FileArtifactMessageList = 2;
    }

    internal static class FileArtifactMessageFieldsId
    {
        public const ushort FullPath = 1;
        public const ushort DisplayName = 2;
        public const ushort Description = 3;
        public const ushort TestUid = 4;
        public const ushort TestDisplayName = 5;
        public const ushort SessionUid = 6;
    }

    internal static class TestSessionEventFieldsId
    {
        public const int MessagesSerializerId = 8;

        public const ushort SessionType = 1;
        public const ushort SessionUid = 2;
        public const ushort ExecutionId = 3;
    }

    internal static class HandshakeMessageFieldsId
    {
        public const int MessagesSerializerId = 9;
    }
}

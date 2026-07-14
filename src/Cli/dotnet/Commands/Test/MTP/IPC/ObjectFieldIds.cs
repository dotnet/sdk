// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// WARNING: Please note this file needs to be kept aligned with the one in the testfx repo.
// It is vendored from microsoft/testfx and tracked in eng/vendored-files.json (see eng/vendored-files.md);
// a scheduled workflow opens an issue when the upstream file changes.
// The protocol follows the concept of optional properties.
// The id is used to identify the property in the stream and it will be skipped if it's not recognized.
// We can add new properties with new ids, but we CANNOT change the existing ids (to support backwards compatibility).

namespace Microsoft.DotNet.Cli.Commands.Test.IPC;

internal static class VoidResponseFieldsId
{
    public const int MessagesSerializerId = 0;
}

internal static class TestHostCompletedRequestFieldsId
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
    // Reserved: field ID 5 was ObsolescenceMessage, removed as unused.
}

// Reserved: serializer ID 4 was ModuleSerializer, removed as unused.

internal static class DiscoveredTestMessagesFieldsId
{
    public const int MessagesSerializerId = 5;

    public const ushort ExecutionId = 1;
    public const ushort InstanceId = 2;
    public const ushort DiscoveredTestMessageList = 3;
}

internal static class DiscoveredTestMessageFieldsId
{
    public const ushort Uid = 1;
    public const ushort DisplayName = 2;
    public const ushort FilePath = 3;
    public const ushort LineNumber = 4;
    public const ushort Namespace = 5;
    public const ushort TypeName = 6;
    public const ushort MethodName = 7;
    public const ushort Traits = 8;
    public const ushort ParameterTypeFullNames = 9;
}

internal static class TraitMessageFieldsId
{
    public const ushort Key = 1;
    public const ushort Value = 2;
}

internal static class TestResultMessagesFieldsId
{
    public const int MessagesSerializerId = 6;

    public const ushort ExecutionId = 1;
    public const ushort InstanceId = 2;
    public const ushort SuccessfulTestMessageList = 3;
    public const ushort FailedTestMessageList = 4;
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

    // Optional assertion diff fields. They carry the structured expected/actual values captured by
    // assertion libraries (e.g. MSTest's Assert stores them on Exception.Data["assert.expected"] /
    // ["assert.actual"]) so the SDK's TerminalTestReporter can render the same expected-vs-actual diff
    // for multi-assembly `dotnet test` runs that it already renders for single-assembly runs. Added
    // after SessionUid; older readers skip unrecognized field ids, so this stays backwards compatible.
    public const ushort Expected = 10;
    public const ushort Actual = 11;
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
    public const ushort InstanceId = 2;
    public const ushort FileArtifactMessageList = 3;
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

internal static class TestInProgressMessagesFieldsId
{
    public const int MessagesSerializerId = 10;

    public const ushort ExecutionId = 1;
    public const ushort InstanceId = 2;
    public const ushort TestInProgressMessageList = 3;
}

internal static class TestInProgressMessageFieldsId
{
    public const ushort Uid = 1;
    public const ushort DisplayName = 2;
}

internal static class AzureDevOpsLogMessageFieldsId
{
    public const int MessagesSerializerId = 11;

    public const ushort ExecutionId = 1;
    public const ushort InstanceId = 2;
    public const ushort LogText = 3;
}

internal static class DisplayMessageFieldsId
{
    public const int MessagesSerializerId = 12;

    public const ushort ExecutionId = 1;
    public const ushort InstanceId = 2;
    public const ushort Level = 3;
    public const ushort Text = 4;
}

// NOTE: Serializer ids 13 (WaitForServerControlRequest) and 14 (ServerControlMessage) exist upstream
// in testfx but are intentionally not vendored here yet: they belong to the reverse server-control pipe
// / server-initiated cancellation feature (protocol 1.4.0), which is out of scope for this contract.

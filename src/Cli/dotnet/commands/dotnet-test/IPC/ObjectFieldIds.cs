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
        internal const int ModulePath = 1;
        internal const int CommandLineOptionMessageList = 2;
    }

    internal static class CommandLineOptionMessageFieldsId
    {
        internal const int Name = 1;
        internal const int Description = 2;
        internal const int IsHidden = 3;
        internal const int IsBuiltIn = 4;
    }

    internal static class SuccessfulTestResultMessageFieldsId
    {
        internal const int Uid = 1;
        internal const int DisplayName = 2;
        internal const int State = 3;
        internal const int Reason = 4;
        internal const int SessionUid = 5;
        internal const int ModulePath = 6;
    }

    internal static class FailedTestResultMessageFieldsId
    {
        internal const int Uid = 1;
        internal const int DisplayName = 2;
        internal const int State = 3;
        internal const int Reason = 4;
        internal const int ErrorMessage = 5;
        internal const int ErrorStackTrace = 6;
        internal const int SessionUid = 7;
        internal const int ModulePath = 8;
    }

    internal static class FileArtifactInfoFieldsId
    {
        internal const int FullPath = 1;
        internal const int DisplayName = 2;
        internal const int Description = 3;
        internal const int TestUid = 4;
        internal const int TestDisplayName = 5;
        internal const int SessionUid = 6;
        internal const int ModulePath = 7;
    }

    internal static class TestSessionEventFieldsId
    {
        internal const int SessionType = 1;
        internal const int SessionUid = 2;
        internal const int ModulePath = 3;
    }
}

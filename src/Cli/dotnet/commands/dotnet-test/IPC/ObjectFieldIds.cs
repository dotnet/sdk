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
        internal const int ModuleName = 1;
        internal const int CommandLineOptionMessageList = 2;
    }

    internal static class CommandLineOptionMessageFieldsId
    {
        internal const int Name = 1;
        internal const int Description = 2;
        internal const int IsHidden = 3;
        internal const int IsBuiltIn = 4;
    }
}

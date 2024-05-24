// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.commands.dotnet_test.IPC
{
    internal class CommandLineOptionMessagesFields
    {
        internal const int ModuleName = 1;
        internal const int CommandLineOptionMessageList = 2;
    }

    internal class CommandLineOptionMessageFields
    {
        internal const int Name = 1;
        internal const int Description = 2;
        internal const int Arity = 3;
        internal const int IsHidden = 4;
        internal const int IsBuiltIn = 5;
    }
}

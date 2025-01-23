// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    internal static class SdkCheckCommandParser
    {
        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("check", LocalizableStrings.AppFullName);

            command.SetAction(SdkCheckCommand.Run);

            return command;
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    internal static class SdkCheckCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("check", LocalizableStrings.AppFullName);

            command.SetAction(SdkCheckCommand.Run);

            return command;
        }
    }
}

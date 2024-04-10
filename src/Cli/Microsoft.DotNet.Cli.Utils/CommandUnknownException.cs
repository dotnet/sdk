// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public class CommandUnknownException : GracefulException
    {
        public string InstructionMessage { get; } = string.Empty;

        public CommandUnknownException(string commandName) : base(
            LocalizableStrings.NoExecutableFoundMatchingCommandErrorMessage)
        {
            InstructionMessage = string.Format(
                LocalizableStrings.NoExecutableFoundMatchingCommand,
                commandName);
        }

        public CommandUnknownException(string commandName, Exception innerException) : base(
            LocalizableStrings.NoExecutableFoundMatchingCommandErrorMessage)
        {
            InstructionMessage = string.Format(
                LocalizableStrings.NoExecutableFoundMatchingCommand,
                commandName);
        }
    }
}

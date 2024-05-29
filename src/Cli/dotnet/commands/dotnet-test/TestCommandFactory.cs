// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class TestCommandFactory
    {
        public static CliCommand Create(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException($"'{nameof(commandName)}' cannot be null or whitespace.", nameof(commandName));
            }

            return new TestingPlatformCommand(commandName);
        }
    }
}

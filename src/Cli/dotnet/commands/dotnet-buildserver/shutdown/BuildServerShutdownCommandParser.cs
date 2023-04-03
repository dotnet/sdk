// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.BuildServer.Shutdown;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.Shutdown.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ServerShutdownCommandParser
    {
        public static readonly CliOption<bool> MSBuildOption = new CliOption<bool>("--msbuild") { Description = LocalizableStrings.MSBuildOptionDescription };
        public static readonly CliOption<bool> VbcsOption = new CliOption<bool>("--vbcscompiler") { Description = LocalizableStrings.VBCSCompilerOptionDescription };
        public static readonly CliOption<bool> RazorOption = new CliOption<bool>("--razor") { Description = LocalizableStrings.RazorOptionDescription};

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new ("shutdown", LocalizableStrings.CommandDescription);

            command.Options.Add(MSBuildOption);
            command.Options.Add(VbcsOption);
            command.Options.Add(RazorOption);

            command.SetAction((parseResult) => new BuildServerShutdownCommand(parseResult).Execute());

            return command;
        }
    }
}

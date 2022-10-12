// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands
{
    internal class CommandResultData
    {
        public CommandResultData(int exitCode, string stdOut, string stdErr, string workingDirectory)
        {
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
            WorkingDirectory = workingDirectory;
        }

        public CommandResultData(CommandResult commandResult)
            : this(commandResult.ExitCode, commandResult.StdOut, commandResult.StdErr, commandResult.StartInfo.WorkingDirectory)
        { }

        public int ExitCode { get; }

        public string StdOut { get; }

        public string StdErr { get; }

        public string WorkingDirectory { get; }
    }
}

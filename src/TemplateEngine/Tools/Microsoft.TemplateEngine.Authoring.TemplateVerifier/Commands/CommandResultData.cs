// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.CommandUtils;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands
{
    internal class CommandResultData : IInstantiationResult
    {
        public CommandResultData(int exitCode, string stdOut, string stdErr, string workingDirectory)
        {
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
            WorkingDirectory = workingDirectory;
        }

        public CommandResultData(CommandResult commandResult)
            : this(commandResult.ExitCode, commandResult.StdOut ?? string.Empty, commandResult.StdErr ?? string.Empty, commandResult.StartInfo.WorkingDirectory)
        { }

        public int ExitCode { get; }

        public string StdOut { get; }

        public string StdErr { get; }

        public string WorkingDirectory { get; }

        public string InstantiatedContentDirectory => WorkingDirectory;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Tools
{
    internal class WorkspaceFormatResult
    {
        public int ExitCode { get; }
        public int FilesFormatted { get; }
        public int FileCount { get; }

        public WorkspaceFormatResult(int filesFormatted, int fileCount, int exitCode)
        {
            FilesFormatted = filesFormatted;
            FileCount = fileCount;
            ExitCode = exitCode;
        }
    }
}

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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

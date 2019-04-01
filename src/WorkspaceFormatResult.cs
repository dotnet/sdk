// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Tools
{
    internal class WorkspaceFormatResult
    {
        public int ExitCode { get; set; }
        public int FilesFormatted { get; set; }
        public int FileCount { get; set; }
    }
}

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class FormatOptions
    {
        public string WorkspaceFilePath { get; }
        public WorkspaceType WorkspaceType { get; }
        public LogLevel LogLevel { get; }
        public bool SaveFormattedFiles { get; }
        public bool ChangesAreErrors { get; }
        public Matcher FileMatcher { get; }
        public string? ReportPath { get; }

        public FormatOptions(
            string workspaceFilePath,
            WorkspaceType workspaceType,
            LogLevel logLevel,
            bool saveFormattedFiles,
            bool changesAreErrors,
            Matcher fileMatcher,
            string? reportPath)
        {
            WorkspaceFilePath = workspaceFilePath;
            WorkspaceType = workspaceType;
            LogLevel = logLevel;
            SaveFormattedFiles = saveFormattedFiles;
            ChangesAreErrors = changesAreErrors;
            FileMatcher = fileMatcher;
            ReportPath = reportPath;
        }

        public void Deconstruct(
            out string workspaceFilePath,
            out WorkspaceType workspaceType,
            out LogLevel logLevel,
            out bool saveFormattedFiles,
            out bool changesAreErrors,
            out Matcher fileMatcher,
            out string? reportPath)
        {
            workspaceFilePath = WorkspaceFilePath;
            workspaceType = WorkspaceType;
            logLevel = LogLevel;
            saveFormattedFiles = SaveFormattedFiles;
            changesAreErrors = ChangesAreErrors;
            fileMatcher = FileMatcher;
            reportPath = ReportPath;
        }
    }
}

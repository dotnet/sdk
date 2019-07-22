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
        public FormatType FormatType { get; }
        public bool SaveFormattedFiles { get; }
        public bool ChangesAreErrors { get; }
        public Matcher FileMatcher { get; }
        public string? ReportPath { get; }
        public bool IncludeGeneratedFiles { get; }

        public FormatOptions(
            string workspaceFilePath,
            WorkspaceType workspaceType,
            LogLevel logLevel,
            FormatType formatType,
            bool saveFormattedFiles,
            bool changesAreErrors,
            Matcher fileMatcher,
            string? reportPath,
            bool includeGeneratedFiles)
        {
            WorkspaceFilePath = workspaceFilePath;
            WorkspaceType = workspaceType;
            LogLevel = logLevel;
            FormatType = formatType;
            SaveFormattedFiles = saveFormattedFiles;
            ChangesAreErrors = changesAreErrors;
            FileMatcher = fileMatcher;
            ReportPath = reportPath;
            IncludeGeneratedFiles = includeGeneratedFiles;
        }

        public void Deconstruct(
            out string workspaceFilePath,
            out WorkspaceType workspaceType,
            out LogLevel logLevel,
            out FormatType formatType,
            out bool saveFormattedFiles,
            out bool changesAreErrors,
            out Matcher fileMatcher,
            out string? reportPath,
            out bool includeGeneratedFiles)
        {
            workspaceFilePath = WorkspaceFilePath;
            workspaceType = WorkspaceType;
            logLevel = LogLevel;
            formatType = FormatType;
            saveFormattedFiles = SaveFormattedFiles;
            changesAreErrors = ChangesAreErrors;
            fileMatcher = FileMatcher;
            reportPath = ReportPath;
            includeGeneratedFiles = IncludeGeneratedFiles;
        }
    }
}

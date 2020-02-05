// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
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
        public ImmutableHashSet<string> FilesToFormat { get; }
        public string ReportPath { get; }

        public FormatOptions(
            string workspaceFilePath,
            WorkspaceType workspaceType,
            LogLevel logLevel,
            bool saveFormattedFiles,
            bool changesAreErrors,
            ImmutableHashSet<string> filesToFormat,
            string reportPath)
        {
            WorkspaceFilePath = workspaceFilePath;
            WorkspaceType = workspaceType;
            LogLevel = logLevel;
            SaveFormattedFiles = saveFormattedFiles;
            ChangesAreErrors = changesAreErrors;
            FilesToFormat = filesToFormat;
            ReportPath = reportPath;
        }

        public void Deconstruct(
            out string workspaceFilePath,
            out WorkspaceType workspaceType,
            out LogLevel logLevel,
            out bool saveFormattedFiles,
            out bool changesAreErrors,
            out ImmutableHashSet<string> filesToFormat,
            out string reportPath)
        {
            workspaceFilePath = WorkspaceFilePath;
            workspaceType = WorkspaceType;
            logLevel = LogLevel;
            saveFormattedFiles = SaveFormattedFiles;
            changesAreErrors = ChangesAreErrors;
            filesToFormat = FilesToFormat;
            reportPath = ReportPath;
        }
    }
}

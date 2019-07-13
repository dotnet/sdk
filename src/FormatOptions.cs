using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class FormatOptions
    {
        public string WorkspaceFilePath { get; }
        public bool IsSolution { get; }
        public LogLevel LogLevel { get; }
        public bool SaveFormattedFiles { get; }
        public bool ChangesAreErrors { get; }
        public ImmutableHashSet<string> FilesToFormat { get; }

        public FormatOptions(
            string workspaceFilePath,
            bool isSolution,
            LogLevel logLevel,
            bool saveFormattedFiles,
            bool changesAreErrors,
            ImmutableHashSet<string> filesToFormat)
        {
            WorkspaceFilePath = workspaceFilePath;
            IsSolution = isSolution;
            LogLevel = logLevel;
            SaveFormattedFiles = saveFormattedFiles;
            ChangesAreErrors = changesAreErrors;
            FilesToFormat = filesToFormat;
        }

        public void Deconstruct(
            out string workspaceFilePath,
            out bool isSolution,
            out LogLevel logLevel,
            out bool saveFormattedFiles,
            out bool changesAreErrors,
            out ImmutableHashSet<string> filesToFormat)
        {
            workspaceFilePath = WorkspaceFilePath;
            isSolution = IsSolution;
            logLevel = LogLevel;
            saveFormattedFiles = SaveFormattedFiles;
            changesAreErrors = ChangesAreErrors;
            filesToFormat = FilesToFormat;
        }
    }
}

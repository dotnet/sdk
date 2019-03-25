namespace Microsoft.CodeAnalysis.Tools
{
    internal class WorkspaceFormatResult
    {
        public int ExitCode { get; set; }
        public int FilesFormatted { get; set; }
        public int FileCount { get; set; }
    }
}

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Tools
{
    public class FormattedFile
    {
        public DocumentId DocumentId { get; set; }

        public string FileName { get; set; }

        public string FilePath { get; set; }

        public IEnumerable<FileChange> FileChanges { get; set; }

        public FormattedFile(Document document, IEnumerable<FileChange> fileChanges)
        {
            DocumentId = document.Id;
            FileName = document.Name;
            FilePath = document.FilePath;
            FileChanges = fileChanges;
        }
    }
}

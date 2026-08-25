// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Tools
{
    public class FormattedFile
    {
        public DocumentId DocumentId { get; }

        public string FileName { get; }

        public string? FilePath { get; }

        public IEnumerable<FileChange> FileChanges { get; }

        public FormattedFile(Document document, IEnumerable<FileChange> fileChanges)
        {
            DocumentId = document.Id;
            FileName = document.Name;
            FilePath = document.FilePath;
            FileChanges = fileChanges;
        }
    }
}

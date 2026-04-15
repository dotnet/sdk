// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal sealed partial class FolderWorkspace : Workspace
    {
        private static Encoding DefaultEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private FolderWorkspace(HostServices hostServices)
            : base(hostServices, "Folder")
        {
        }

        public static FolderWorkspace Create()
        {
            return Create(MefHostServices.DefaultHost);
        }

        public static FolderWorkspace Create(HostServices hostServices)
        {
            return new FolderWorkspace(hostServices);
        }

        public Solution OpenFolder(string folderPath, SourceFileMatcher fileMatcher)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                throw new ArgumentException($"Folder '{folderPath}' does not exist.", nameof(folderPath));
            }

            ClearSolution();

            var solutionInfo = FolderSolutionLoader.LoadSolutionInfo(folderPath, fileMatcher);

            OnSolutionAdded(solutionInfo);

            return CurrentSolution;
        }

        public override bool CanApplyChange(ApplyChangesKind feature)
        {
            // Whitespace formatting should only produce document changes; no other types of changes are supported.
            return feature == ApplyChangesKind.ChangeDocument;
        }

        protected override void ApplyDocumentTextChanged(DocumentId documentId, SourceText text)
        {
            var document = CurrentSolution.GetDocument(documentId);
            if (document?.FilePath != null && text.Encoding != null)
            {
                SaveDocumentText(documentId, document.FilePath, text, text.Encoding);
                OnDocumentTextChanged(documentId, text, PreservationMode.PreserveValue);
            }
        }

        private void SaveDocumentText(DocumentId id, string fullPath, SourceText newText, Encoding encoding)
        {
            try
            {
                using var writer = new StreamWriter(fullPath, append: false, encoding);
                newText.Write(writer);
            }
            catch (IOException exception)
            {
                OnWorkspaceFailed(new DocumentDiagnostic(WorkspaceDiagnosticKind.Failure, exception.Message, id));
            }
        }
    }
}

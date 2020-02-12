// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Tools.Workspaces
{
    internal sealed partial class FolderWorkspace : Workspace
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private FolderWorkspace(HostServices hostServices)
            : base(hostServices, "Folder")
        {
        }

        public static FolderWorkspace Create()
        {
            return Create(MSBuildMefHostServices.DefaultServices);
        }

        public static FolderWorkspace Create(HostServices hostServices)
        {
            return new FolderWorkspace(hostServices);
        }

        public async Task<Solution> OpenFolder(string folderPath, ImmutableHashSet<string> pathsToInclude, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                throw new ArgumentException($"Folder '{folderPath}' does not exist.", nameof(folderPath));
            }

            ClearSolution();

            var solutionInfo = await FolderSolutionLoader.LoadSolutionInfoAsync(folderPath, pathsToInclude, cancellationToken).ConfigureAwait(false);

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
            var document = this.CurrentSolution.GetDocument(documentId);
            if (document != null)
            {
                SaveDocumentText(documentId, document.FilePath, text, text.Encoding);
                OnDocumentTextChanged(documentId, text, PreservationMode.PreserveValue);
            }
        }

        private void SaveDocumentText(DocumentId id, string fullPath, SourceText newText, Encoding encoding)
        {
            try
            {
                using (var writer = new StreamWriter(fullPath, append: false, encoding))
                {
                    newText.Write(writer);
                }
            }
            catch (IOException exception)
            {
                OnWorkspaceFailed(new DocumentDiagnostic(WorkspaceDiagnosticKind.Failure, exception.Message, id));
            }
        }
    }
}

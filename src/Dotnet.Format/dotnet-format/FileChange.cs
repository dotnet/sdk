// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Tools
{
    public class FileChange
    {
        public int LineNumber { get; }

        public int CharNumber { get; }

        public string DiagnosticId { get; }

        public string FormatDescription { get; }

        public FileChange(LinePosition changePosition, string diagnosticId, string formatDescription)
        {
            // LinePosition is zero based so we need to increment to report numbers people expect.
            LineNumber = changePosition.Line + 1;
            CharNumber = changePosition.Character + 1;
            DiagnosticId = diagnosticId;
            FormatDescription = formatDescription;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal sealed class MSBuildIssueFormatter : IIssueFormatter
    {
        public string FormatIssue(Document document, string severity, string issueId, int lineNumber, int charNumber, string message)
            => $"{document.FilePath ?? document.Name}({lineNumber},{charNumber}): {severity} {issueId}: {message} [{document.Project.FilePath}]";
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    public interface IIssueFormatter
    {
        string FormatIssue(Document document, string severity, string issueId, int lineNumber, int charNumber, string message);
    }
}

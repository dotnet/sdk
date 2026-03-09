// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    public interface IIssueFormatter
    {
        string FormatIssue(Document document, string severity, string issueId, int lineNumber, int charNumber, string message);
    }
}

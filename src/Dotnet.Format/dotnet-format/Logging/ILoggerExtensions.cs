// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class ILoggerExtensions
    {
        private static readonly string s_errorSeverityString = DiagnosticSeverity.Error.ToString().ToLower();

        public static IIssueFormatter IssueFormatter { get; set; } = new MSBuildIssueFormatter();

        public static string LogFormattingIssue(this ILogger logger, Document document, string formatterName, FileChange fileChange, bool changesAreErrors)
            => LogIssue(logger, document, s_errorSeverityString, formatterName, fileChange.LineNumber, fileChange.CharNumber, fileChange.FormatDescription, changesAreErrors);

        public static string LogDiagnosticIssue(this ILogger logger, Document document, LinePosition diagnosticPosition, Diagnostic diagnostic, bool changesAreErrors)
            => LogIssue(logger, document, diagnostic.Severity.ToString().ToLower(), diagnostic.Id, diagnosticPosition.Line + 1, diagnosticPosition.Character + 1, diagnostic.GetMessage(), changesAreErrors);

        private static string LogIssue(ILogger logger, Document document, string severity, string issueId, int lineNumber, int charNumber, string message, bool changesAreErrors)
        {
            var formattedMessage = IssueFormatter.FormatIssue(document, severity, issueId, lineNumber, charNumber, message);

            if (changesAreErrors)
            {
                logger.LogError(formattedMessage);
            }
            else
            {
                logger.LogWarning(formattedMessage);
            }

            return formattedMessage;
        }
    }
}

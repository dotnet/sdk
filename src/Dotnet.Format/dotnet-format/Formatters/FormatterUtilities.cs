// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    internal static class FormatterUtilities
    {
        public static ImmutableArray<FileChange> GetFileChanges(
            TextDocument document,
            SourceText originalText,
            SourceText formattedText,
            string formatterName,
            string formatWarningDescription,
            FormatOptions formatOptions,
            ILogger logger)
        {
            var fileChanges = ImmutableArray.CreateBuilder<FileChange>();
            var changes = formattedText.GetTextChanges(originalText);

            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index];

                var changeMessage = changes.Count > 1 || change.NewText?.Length != formattedText.Length
                    ? BuildChangeMessage(change)
                    : string.Empty;

                var changePosition = originalText.Lines.GetLinePosition(change.Span.Start);

                var fileChange = new FileChange(changePosition, formatterName, $"{formatWarningDescription}{changeMessage}");
                fileChanges.Add(fileChange);

                if (!formatOptions.SaveFormattedFiles || formatOptions.LogLevel == LogLevel.Debug)
                {
                    logger.LogFormattingIssue(document, formatterName, fileChange, formatOptions.ChangesAreErrors);
                }
            }

            return fileChanges.ToImmutable();
        }

        private static string BuildChangeMessage(TextChange change)
        {
            var isDelete = string.IsNullOrEmpty(change.NewText);
            var isAdd = change.Span.Length == 0;
            if (isDelete && isAdd)
            {
                return string.Empty;
            }

            // Escape characters in the text changes so that it can be more easily read.
            var textChange = change.NewText?.Replace(" ", "\\s").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
            var message = isDelete
                ? string.Format(Resources.Delete_0_characters, change.Span.Length)
                : isAdd
                    ? string.Format(Resources.Insert_0, textChange)
                    : string.Format(Resources.Replace_0_characters_with_1, change.Span.Length, textChange);
            return $" {message}";
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    /// <summary>
    /// Shared logic for detecting the deprecated unquoted-whitespace form of a file-based program
    /// <c>#:</c> directive and for computing its quoted replacement. This mirrors (a conservative
    /// subset of) the directive parser in <c>Microsoft.DotNet.FileBasedPrograms</c> without taking a
    /// dependency on it: it flags only directives that the parser accepts as the legacy form and that
    /// have an unambiguous, semantics-preserving quoted equivalent.
    /// </summary>
    internal static class FileBasedProgramDirectiveQuoting
    {
        // Characters that are not allowed in a directive name (matches the parser's DisallowedNameCharacters).
        private static readonly char[] s_disallowedNameCharacters = [' ', '\t', '\n', '\r', '\f', '\v', '@', '=', '/'];

        /// <summary>
        /// Extracts the directive kind (e.g. <c>property</c>) and its value text from a file-based program
        /// <c>#:</c> directive trivia. Returns <see langword="false"/> for any other trivia.
        /// </summary>
        public static bool TryParse(SyntaxTrivia trivia, out string kind, out string value)
        {
            kind = string.Empty;
            value = string.Empty;

            // '#:' directives are represented as directive trivia whose structure carries a single
            // string literal token holding the text after '#:'. Exclude the '#!' shebang explicitly.
            if (trivia.IsKind(SyntaxKind.ShebangDirectiveTrivia))
            {
                return false;
            }

            var structure = trivia.GetStructure();
            if (structure is null)
            {
                return false;
            }

            var content = structure.ChildTokens().FirstOrDefault(static token => token.IsKind(SyntaxKind.StringLiteralToken));
            if (!content.IsKind(SyntaxKind.StringLiteralToken))
            {
                return false;
            }

            var text = content.Text.Trim();
            if (text.Length == 0)
            {
                return false;
            }

            var whitespaceIndex = IndexOfWhitespace(text);
            if (whitespaceIndex < 0)
            {
                kind = text;
                value = string.Empty;
            }
            else
            {
                kind = text.Substring(0, whitespaceIndex);
                value = text.Substring(whitespaceIndex).TrimStart();
            }

            return true;
        }

        /// <summary>
        /// Returns whether the directive uses the deprecated unquoted-whitespace form and, if so,
        /// computes the equivalent quoted <paramref name="newValue"/> (the text that should follow the
        /// directive kind).
        /// </summary>
        public static bool TryGetQuotedForm(string kind, string value, out string newValue)
        {
            newValue = value;

            // No value, or already quoted (quotes unambiguously mean the new form): nothing to flag.
            if (value.Length == 0 || value.IndexOf('"') >= 0)
            {
                return false;
            }

            var tokens = SplitWhitespace(value);

            // A single whitespace-separated token is unambiguous and never the legacy form.
            if (tokens.Count <= 1)
            {
                return false;
            }

            switch (kind)
            {
                case "property":
                    // Value after the first '='; the name must be valid so this is deprecated (not invalid).
                    return TryQuoteAfterSeparator(value, out newValue);

                case "sdk":
                case "package":
                    // A trailing run of valid 'Name=Value' tokens is the new metadata form, not legacy.
                    if (kind == "package" && AllMetadataLike(tokens))
                    {
                        return false;
                    }

                    return TryCollapseNameAndVersion(value, out newValue);

                case "project":
                case "ref":
                    if (AllMetadataLike(tokens))
                    {
                        return false;
                    }

                    newValue = Quote(value);
                    return true;

                case "include":
                case "exclude":
                    newValue = Quote(value);
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryQuoteAfterSeparator(string value, out string newValue)
        {
            newValue = value;

            var separatorIndex = value.IndexOf('=');
            if (separatorIndex < 0)
            {
                return false;
            }

            var name = value.Substring(0, separatorIndex).TrimEnd();
            if (name.Length == 0 || name.IndexOfAny(s_disallowedNameCharacters) >= 0)
            {
                return false;
            }

            var innerValue = value.Substring(separatorIndex + 1).TrimStart();
            newValue = name + "=" + QuoteIfNeeded(innerValue);
            return true;
        }

        private static bool TryCollapseNameAndVersion(string value, out string newValue)
        {
            newValue = value;

            var separatorIndex = value.IndexOf('@');
            if (separatorIndex < 0)
            {
                return false;
            }

            var name = value.Substring(0, separatorIndex).TrimEnd();
            if (name.Length == 0 || name.IndexOfAny(s_disallowedNameCharacters) >= 0)
            {
                return false;
            }

            // The version follows '@'; the parser does not allow quoting there, so a version with internal
            // whitespace has no valid quoted form and is left alone (it is a broken version anyway).
            var version = value.Substring(separatorIndex + 1).TrimStart();
            if (version.Length == 0 || IndexOfWhitespace(version) >= 0)
            {
                return false;
            }

            newValue = name + "@" + version;
            return true;
        }

        private static bool AllMetadataLike(List<string> tokens)
        {
            for (var i = 1; i < tokens.Count; i++)
            {
                if (tokens[i].IndexOf('=') <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static string QuoteIfNeeded(string value)
        {
            return IndexOfWhitespace(value) >= 0 ? Quote(value) : value;
        }

        private static string Quote(string value) => "\"" + value + "\"";

        private static int IndexOfWhitespace(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<string> SplitWhitespace(string text)
        {
            var tokens = new List<string>();
            var start = -1;
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    if (start >= 0)
                    {
                        tokens.Add(text.Substring(start, i - start));
                        start = -1;
                    }
                }
                else if (start < 0)
                {
                    start = i;
                }
            }

            if (start >= 0)
            {
                tokens.Add(text.Substring(start));
            }

            return tokens;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.FileBasedPrograms;

/// <summary>
/// Low-level primitives for parsing and formatting the values of file-based program <c>#:</c>
/// directives. These are source-shared between the CLI directive parser
/// (<c>FileLevelDirectiveHelpers</c>) and the analyzer that flags the deprecated unquoted form
/// (<c>FileBasedProgramDirectiveQuoting</c>), so both agree on quoting, name validity, and metadata
/// detection instead of each duplicating the logic.
/// </summary>
internal static class FileBasedProgramDirectiveValueHelpers
{
    // Characters that are not allowed in a directive or metadata name because they would be confused
    // with a separator: whitespace, '@', '=', '/'.
    private static readonly Regex s_disallowedNameCharacters = new("""[\s@=/]""", RegexOptions.Compiled);

    /// <summary>
    /// Returns whether <paramref name="name"/> contains a character that is not allowed in a directive
    /// or metadata name (whitespace or one of the separator characters <c>@</c>, <c>=</c>, <c>/</c>).
    /// </summary>
    public static bool ContainsDisallowedNameCharacter(string name) => s_disallowedNameCharacters.IsMatch(name);

    /// <summary>
    /// Validates that <paramref name="name"/> is a valid XML NCName, the constraint MSBuild applies to
    /// property and item-metadata names (an NCName additionally disallows the ':' that a plain XML name
    /// permits). Returns <see langword="true"/> when valid; otherwise returns <see langword="false"/> and
    /// sets <paramref name="errorMessage"/> to the underlying validation-failure message.
    /// </summary>
    public static bool IsValidMSBuildName(string name, out string? errorMessage)
    {
        try
        {
            XmlConvert.VerifyNCName(name);
            errorMessage = null;
            return true;
        }
        catch (XmlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Returns whether every token from <paramref name="start"/> onwards is a valid <c>Name=Value</c>
    /// item-metadata pair (a valid MSBuild name, then <c>'='</c>, then any value).
    /// </summary>
    public static bool AllValidMetadata(IReadOnlyList<string> tokens, int start)
    {
        for (var i = start; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var separatorIndex = token.IndexOf('=');
            if (separatorIndex <= 0)
            {
                return false;
            }

            if (!IsValidMSBuildName(token.Substring(0, separatorIndex), out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Wraps <paramref name="value"/> in a C# string literal when it contains a character (whitespace or
    /// a double quote) that cannot appear in a bare directive token, so it round-trips through the parser
    /// (which lexes a quoted value as a regular C# string literal). Otherwise returns it unchanged.
    /// </summary>
    public static string QuoteIfNeeded(string value)
    {
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || c == '"')
            {
                // FormatLiteral produces a properly escaped C# string literal (e.g. "a\"b", "a\tb") that
                // the parser decodes back to the original value.
                return SymbolDisplay.FormatLiteral(value, quote: true);
            }
        }

        return value;
    }
}

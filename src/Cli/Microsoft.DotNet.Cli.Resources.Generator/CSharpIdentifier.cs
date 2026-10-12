// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Converts resource names to valid escaped C# identifiers.
/// </summary>
internal static class CSharpIdentifier
{
    /// <summary>
    ///  Converts a resource key to a C# identifier.
    /// </summary>
    /// <param name="name">The resource key to convert.</param>
    /// <returns>A valid identifier with unsupported characters replaced by underscores.</returns>
    internal static string FromResourceName(string name)
    {
        using ValueStringBuilder builder = new(stackalloc char[256]);
        if (!IsIdentifierStartCharacter(name[0]))
        {
            builder.Append('_');
        }

        foreach (char character in name)
        {
            builder.Append(IsIdentifierPartCharacter(character) ? character : '_');
        }

        return builder.ToString();
    }

    /// <summary>
    ///  Determines whether a namespace consists only of valid C# identifiers.
    /// </summary>
    /// <param name="namespaceName">
    ///  The namespace to validate, or <see langword="null"/> for the global namespace.
    /// </param>
    /// <returns><see langword="true"/> when the namespace is valid; otherwise, <see langword="false"/>.</returns>
    internal static bool IsValidNamespace(string? namespaceName)
    {
        if (namespaceName is null)
        {
            return true;
        }

        if (namespaceName.Length == 0)
        {
            return false;
        }

        string[] parts = namespaceName.Split('.');
        foreach (string part in parts)
        {
            if (!IsValidIdentifier(part))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Determines whether a value can be emitted as a C# identifier.
    /// </summary>
    /// <param name="identifier">The identifier to validate.</param>
    /// <returns>
    ///  <see langword="true"/> when the value is a valid identifier or keyword; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    internal static bool IsValidIdentifier(string identifier)
    {
        return SyntaxFacts.IsValidIdentifier(identifier)
            || SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None;
    }

            /// <summary>
            ///  Escapes an identifier so that C# keywords remain valid member names.
            /// </summary>
            /// <param name="identifier">The identifier to escape.</param>
            /// <returns>The verbatim identifier.</returns>
    internal static string Escape(string identifier) => "@" + identifier;

            /// <summary>
            ///  Escapes every identifier in a namespace.
            /// </summary>
            /// <param name="namespaceName">The namespace to escape.</param>
            /// <returns>The namespace with each component escaped.</returns>
    internal static string EscapeNamespace(string namespaceName)
    {
        string[] parts = namespaceName.Split('.');
        for (int index = 0; index < parts.Length; index++)
        {
            parts[index] = Escape(parts[index]);
        }

        return string.Join(".", parts);
    }

    /// <summary>
    ///  Determines whether a character can appear after the first character of a C# identifier.
    /// </summary>
    /// <param name="character">The character to inspect.</param>
    /// <returns><see langword="true"/> when the character is valid; otherwise, <see langword="false"/>.</returns>
    internal static bool IsIdentifierPartCharacter(char character)
    {
        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
        return IsLetter(category)
            || category == UnicodeCategory.DecimalDigitNumber
            || category == UnicodeCategory.ConnectorPunctuation
            || category == UnicodeCategory.Format
            || category == UnicodeCategory.NonSpacingMark
            || category == UnicodeCategory.SpacingCombiningMark;
    }

    private static bool IsIdentifierStartCharacter(char character)
    {
        return character == '_'
            || IsLetter(CharUnicodeInfo.GetUnicodeCategory(character));
    }

    private static bool IsLetter(UnicodeCategory category)
    {
        return category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber;
    }
}
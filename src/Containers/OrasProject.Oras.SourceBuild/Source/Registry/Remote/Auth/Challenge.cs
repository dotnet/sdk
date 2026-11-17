// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/Challenge.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Represents a parsed registry <c>WWW-Authenticate</c> challenge, and provides
/// factory methods for parsing challenge headers.
/// </summary>
/// <param name="Scheme">The parsed authentication <see cref="ChallengeScheme"/>.</param>
/// <param name="Parameters">
/// The parsed Bearer parameters, or <c>null</c> when the scheme is not
/// <see cref="ChallengeScheme.Bearer"/> or no parameters are present. A repeated key keeps its last value.
/// </param>
public readonly record struct Challenge(
    ChallengeScheme Scheme,
    IReadOnlyDictionary<string, string>? Parameters)
{
    private const string _specialChars = "!#$%&'*+-.^_`|~";

    /// <summary>
    /// Parses the "WWW-Authenticate" header returned by the remote registry
    /// and extracts parameters if scheme is Bearer.
    ///
    /// Reference:
    /// - https://datatracker.ietf.org/doc/html/rfc7235#section-2.1
    /// </summary>
    /// <param name="header">The authentication challenge header string.</param>
    /// <returns>The parsed <see cref="Challenge"/>.</returns>
    /// <exception cref="FormatException">Thrown when a quoted parameter value is not properly closed.</exception>
    public static Challenge Parse(string? header)
    {
        if (!TryParse(header, out var challenge))
        {
            throw new FormatException("Quoted parameter value is not properly closed.");
        }
        return challenge;
    }

    /// <summary>
    /// Attempts to parse the "WWW-Authenticate" header returned by the remote registry, extracting
    /// Bearer parameters, without throwing on a malformed challenge.
    /// </summary>
    /// <param name="header">The authentication challenge header string.</param>
    /// <param name="challenge">
    /// The parsed <see cref="Challenge"/>. On a malformed challenge this is still set, carrying
    /// the parsed <see cref="ChallengeScheme"/> with <c>null</c> parameters.
    /// </param>
    /// <returns>
    /// <c>true</c> when the challenge is not malformed and was consumed without error (including a
    /// <c>null</c> <paramref name="header"/> or any non-Bearer scheme, so <c>true</c> does not mean a
    /// challenge was present or that Bearer parameters were extracted); <c>false</c> only when a quoted
    /// parameter value is not properly closed, which makes the whole challenge unusable.
    /// </returns>
    public static bool TryParse(string? header, out Challenge challenge)
    {
        var isUsable = TryParseCore(header, out var scheme, out var parameters);
        challenge = new Challenge(scheme, parameters);
        return isUsable;
    }

    /// <summary>
    /// TryParseCore parses the "WWW-Authenticate" header returned by the remote registry and
    /// extracts parameters if the scheme is Bearer, without throwing on a malformed challenge.
    ///
    /// Reference:
    /// - https://datatracker.ietf.org/doc/html/rfc7235#section-2.1
    /// </summary>
    /// <param name="header">The authentication challenge header string.</param>
    /// <param name="scheme">
    /// The parsed <see cref="ChallengeScheme"/>; set even on failure and may be <see cref="ChallengeScheme.Unknown"/>.
    /// </param>
    /// <param name="parameters">
    /// The parsed Bearer parameters, or <c>null</c> when the scheme is not Bearer or none are present.
    /// A repeated parameter key keeps its last value.
    /// </param>
    /// <returns>
    /// <c>true</c> when the challenge is not malformed and was consumed without error. This includes a
    /// <c>null</c> <paramref name="header"/> (no challenge; <paramref name="scheme"/> becomes
    /// <see cref="ChallengeScheme.Unknown"/>) and any non-Bearer scheme, so <c>true</c> does not mean a challenge
    /// was present or that Bearer <paramref name="parameters"/> were extracted. <c>false</c> is returned
    /// only when a quoted parameter value is not properly closed, which makes the whole challenge unusable.
    /// </returns>
    private static bool TryParseCore(
        string? header,
        out ChallengeScheme scheme,
        out Dictionary<string, string>? parameters)
    {
        parameters = null;
        if (header == null)
        {
            scheme = ChallengeScheme.Unknown;
            return true;
        }
        // as defined in RFC 7235 section 2.1, we have
        //     challenge   = auth-scheme [ 1*SP ( token68 / #auth-param ) ]
        //     auth-scheme = token
        //     auth-param  = token BWS "=" BWS ( token / quoted-string )
        //
        // since we focus parameters only on Bearer, we have
        //     challenge   = auth-scheme [ 1*SP #auth-param ]
        var (schemeString, rest) = ParseToken(header);
        scheme = ParseScheme(schemeString);

        if (scheme != ChallengeScheme.Bearer)
        {
            return true;
        }

        rest = rest.Trim();
        if (string.IsNullOrEmpty(rest))
        {
            return true;
        }

        var paramsDictionary = new Dictionary<string, string>();

        // parse params for bearer auth.
        // combining RFC 7235 section 2.1 with RFC 7230 section 7, we have
        //     #auth-param => auth-param *( OWS "," OWS auth-param )
        while (!string.IsNullOrEmpty(rest))
        {
            // split the rest string by non-token char
            var (key, remaining) = ParseToken(rest.Trim());

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(remaining) || !remaining.StartsWith("="))
            {
                break;
            }
            remaining = remaining.Substring(1).Trim();
            string value;

            // extract value if it is quoted
            if (remaining.StartsWith('"'))
            {
                var nextQuote = remaining.IndexOf('"', 1);
                if (nextQuote == -1)
                {
                    // An unterminated quoted value makes the whole challenge unusable.
                    return false;
                }

                value = remaining.Substring(1, nextQuote - 1).Trim();
                rest = remaining.Substring(nextQuote + 1).Trim();
            }
            else
            {
                (value, rest) = ParseToken(remaining.Trim());
                if (string.IsNullOrEmpty(value))
                {
                    break;
                }
            }
            // Keep the last value for a repeated key rather than rejecting the whole challenge.
            paramsDictionary[key] = value;

            rest = rest.Trim();
            if (string.IsNullOrEmpty(rest) || !rest.StartsWith(','))
            {
                break;
            }

            rest = rest.TrimStart(',');
        }

        parameters = paramsDictionary;
        return true;
    }

    /// <summary>
    /// ParseScheme parses the authentication scheme from a string.
    /// </summary>
    /// <param name="schemeString">The string representation of the scheme.</param>
    /// <returns>
    /// The corresponding <see cref="ChallengeScheme"/> value, or <see cref="ChallengeScheme.Unknown"/> 
    /// if the scheme is not recognized.
    /// </returns>
    internal static ChallengeScheme ParseScheme(string schemeString)
    {
        return schemeString.ToLowerInvariant() switch
        {
            "basic" => ChallengeScheme.Basic,
            "bearer" => ChallengeScheme.Bearer,
            _ => ChallengeScheme.Unknown,
        };
    }

    /// <summary>
    /// ParseToken parses a token from a string, splitting it into the token and the remaining string by non-token char.
    /// </summary>
    /// <param name="token">The token string to parse.</param>
    /// <returns>
    /// A tuple containing the parsed token and the remaining string.
    /// </returns>
    internal static (string Token, string Remaining) ParseToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return (string.Empty, string.Empty);
        }

        var index = 0;
        while (index < token.Length && IsValidTokenChar(token[index]))
        {
            index++;
        }

        return (token[..index], token[index..]);
    }

    /// <summary>
    /// IsValidTokenChar determines whether a character is a valid token character defined in RFC 7230 section 3.2.6.
    /// </summary>
    /// <param name="c">The character to check.</param>
    /// <returns>
    /// <c>true</c> if the character is not a valid token character; otherwise, <c>false</c>.
    /// </returns>
    internal static bool IsValidTokenChar(char c) =>
        ('A' <= c && c <= 'Z') ||
        ('a' <= c && c <= 'z') ||
        ('0' <= c && c <= '9') ||
        _specialChars.Contains(c);
}

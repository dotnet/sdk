// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Strips CLI option prefixes like <c>-</c>, <c>--</c>, or <c>/</c> from a string to reveal the user-facing name.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string RemovePrefix(this string name)
    {
        int prefixLength = GetPrefixLength(name);

        return prefixLength > 0 ? name.Substring(prefixLength) : name;

        static int GetPrefixLength(string name)
        {
            if (name[0] == '-')
            {
                return name.Length > 1 && name[1] == '-' ? 2 : 1;
            }

            if (name[0] == '/')
            {
                return 1;
            }

            return 0;
        }
    }

    /// <summary>
    /// Converts a string to camel case using the JSON naming policy. Camel-case means that the first letter of the string is lowercase, and the first letter of each subsequent word is uppercase.
    /// </summary>
    /// <param name="value">A string to ensure is camel-cased</param>
    /// <returns>The camel-cased string</returns>
    public static string ToCamelCase(this string value) => JsonNamingPolicy.CamelCase.ConvertName(value);
}

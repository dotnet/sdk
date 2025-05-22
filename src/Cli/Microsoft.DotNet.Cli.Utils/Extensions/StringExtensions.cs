// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.Cli.Utils.Extensions;

public static class StringExtensions
{
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

    // https://stackoverflow.com/a/66342091/294804
    public static string ToCamelCase(this string value) => JsonNamingPolicy.CamelCase.ConvertName(value);
}

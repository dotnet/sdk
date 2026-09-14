// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

internal static class MSBuildArgumentParser
{
    internal static bool IsMultiThreadedSwitch(string argument)
    {
        string unquoted = Unquote(argument);
        int prefixLength = unquoted.StartsWith("--", StringComparison.Ordinal) ? 2
            : unquoted.StartsWith('-') || unquoted.StartsWith('/') ? 1
            : 0;

        if (prefixLength == 0)
        {
            return false;
        }

        int separatorIndex = unquoted.IndexOf(':');
        string name = separatorIndex < 0 ? unquoted[prefixLength..] : unquoted[prefixLength..separatorIndex];
        if (!name.Equals("mt", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("multiThreaded", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (separatorIndex < 0)
        {
            return true;
        }

        string parameter = unquoted[(separatorIndex + 1)..];
        return parameter.Length == 0 || bool.TryParse(parameter, out _);
    }

    // Match MSBuild's QuotingUtilities.Unquote, including escaped and doubled quotes.
    // https://github.com/dotnet/msbuild/blob/main/src/Framework/Utilities/QuotingUtilities.cs
    private static string Unquote(string argument)
    {
        if (!argument.Contains('"'))
        {
            return argument;
        }

        var result = new StringBuilder(argument.Length);
        bool isQuoted = false;
        int precedingBackslashes = 0;

        for (int i = 0; i < argument.Length; i++)
        {
            switch (argument[i])
            {
                case '\\':
                    precedingBackslashes++;
                    break;

                case '"':
                    result.Append('\\', precedingBackslashes / 2);
                    if (precedingBackslashes % 2 == 0)
                    {
                        if (isQuoted && i + 1 < argument.Length && argument[i + 1] == '"')
                        {
                            result.Append('"');
                            i++;
                        }

                        isQuoted = !isQuoted;
                    }
                    else
                    {
                        result.Append('"');
                    }

                    precedingBackslashes = 0;
                    break;

                default:
                    result.Append('\\', precedingBackslashes);
                    result.Append(argument[i]);
                    precedingBackslashes = 0;
                    break;
            }
        }

        return result.Append('\\', precedingBackslashes).ToString();
    }
}

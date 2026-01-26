// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class VSTestOptions
{
    private static string GetSemiColonEscapedstring(string arg)
    {
        if (arg.IndexOf(";") != -1)
        {
            return arg.Replace(";", "%3b");
        }

        return arg;
    }

    internal static string[] GetSemiColonEscapedArgs(IEnumerable<string> args)
    {
        int counter = 0;
        string[] array = new string[args.Count()];

        foreach (string arg in args)
        {
            array[counter++] = GetSemiColonEscapedstring(arg);
        }

        return array;
    }

    /// <summary>
    /// Adding double quotes around the property helps MSBuild arguments parser and avoid incorrect splits on ',' or ';'.
    /// </summary>
    internal /* for testing purposes */ static string SurroundWithDoubleQuotes(string input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        // If already escaped by double quotes then return original string.
        if (input.StartsWith("\"", StringComparison.Ordinal)
            && input.EndsWith("\"", StringComparison.Ordinal))
        {
            return input;
        }

        // We want to count the number of trailing backslashes to ensure
        // we will have an even number before adding the final double quote.
        // Otherwise the last \" will be interpreted as escaping the double
        // quote rather than a backslash and a double quote.
        var trailingBackslashesCount = 0;
        for (int i = input.Length - 1; i >= 0; i--)
        {
            if (input[i] == '\\')
            {
                trailingBackslashesCount++;
            }
            else
            {
                break;
            }
        }

        return trailingBackslashesCount % 2 == 0
            ? string.Concat("\"", input, "\"")
            : string.Concat("\"", input, "\\\"");
    }
}

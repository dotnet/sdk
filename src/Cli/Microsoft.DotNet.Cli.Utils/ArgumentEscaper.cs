// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils;

public static class ArgumentEscaper
{
    /// <summary>
    /// Undo the processing which took place to create string[] args in Main,
    /// so that the next process will receive the same string[] args
    /// 
    /// See here for more info:
    /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static string EscapeAndConcatenateArgArrayForProcessStart(IEnumerable<string> args)
    {
        var escaped = EscapeArgArray(args);
#if NET35
        return string.Join(" ", escaped.ToArray());
#else
        return string.Join(" ", escaped);
#endif
    }

    /// <summary>
    /// Undo the processing which took place to create string[] args in Main,
    /// so that the next process will receive the same string[] args
    /// 
    /// See here for more info:
    /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static string EscapeAndConcatenateArgArrayForCmdProcessStart(IEnumerable<string> args)
    {
        var escaped = EscapeArgArrayForCmd(args);
#if NET35
        return string.Join(" ", escaped.ToArray());
#else
        return string.Join(" ", escaped);
#endif
    }

    /// <summary>
    /// Undo the processing which took place to create string[] args in Main,
    /// so that the next process will receive the same string[] args
    /// 
    /// See here for more info:
    /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    private static IEnumerable<string> EscapeArgArray(IEnumerable<string> args)
    {
        var escapedArgs = new List<string>();

        foreach (var arg in args)
        {
            escapedArgs.Add(EscapeSingleArg(arg));
        }

        return escapedArgs;
    }

    /// <summary>
    /// This prefixes every character with the '^' character to force cmd to
    /// interpret the argument string literally. An alternative option would 
    /// be to do this only for cmd metacharacters.
    /// 
    /// See here for more info:
    /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    private static IEnumerable<string> EscapeArgArrayForCmd(IEnumerable<string> arguments)
    {
        var escapedArgs = new List<string>();

        foreach (var arg in arguments)
        {
            escapedArgs.Add(EscapeArgForCmd(arg));
        }

        return escapedArgs;
    }

    public static string EscapeSingleArg(string arg, Func<string, bool>? additionalShouldSurroundWithQuotes = null)
    {
        var sb = new StringBuilder();

        var length = arg.Length;
        var needsQuotes = length == 0 || ShouldSurroundWithQuotes(arg) || additionalShouldSurroundWithQuotes?.Invoke(arg) == true;
        var isQuoted = needsQuotes || IsSurroundedWithQuotes(arg);

        if (needsQuotes) sb.Append("\"");

        for (int i = 0; i < length; ++i)
        {
            var backslashCount = 0;

            // Consume All Backslashes
            while (i < arg.Length && arg[i] == '\\')
            {
                backslashCount++;
                i++;
            }

            // Escape any backslashes at the end of the arg
            // when the argument is also quoted.
            // This ensures the outside quote is interpreted as
            // an argument delimiter
            if (i == arg.Length && isQuoted)
            {
                sb.Append('\\', 2 * backslashCount);
            }

            // At then end of the arg, which isn't quoted,
            // just add the backslashes, no need to escape
            else if (i == arg.Length)
            {
                sb.Append('\\', backslashCount);
            }

            // Escape any preceding backslashes and the quote
            else if (arg[i] == '"')
            {
                sb.Append('\\', (2 * backslashCount) + 1);
                sb.Append('"');
            }

            // Output any consumed backslashes and the character
            else
            {
                sb.Append('\\', backslashCount);
                sb.Append(arg[i]);
            }
        }

        if (needsQuotes) sb.Append("\"");

        return sb.ToString();
    }

    /// <summary>
    /// Prepare as single argument to 
    /// roundtrip properly through cmd.
    /// 
    /// This prefixes every character with the '^' character to force cmd to
    /// interpret the argument string literally. An alternative option would 
    /// be to do this only for cmd metacharacters.
    /// 
    /// See here for more info:
    /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    private static string EscapeArgForCmd(string argument)
    {
        var sb = new StringBuilder();

        var quoted = ShouldSurroundWithQuotes(argument);

        if (quoted) sb.Append("^\"");

        // Prepend every character with ^
        // This is harmless when passing through cmd
        // and ensures cmd metacharacters are not interpreted
        // as such
        foreach (var character in argument)
        {
            sb.Append("^");
            sb.Append(character);
        }

        if (quoted) sb.Append("^\"");

        return sb.ToString();
    }

    internal static bool ShouldSurroundWithQuotes(string argument) =>
        // Only quote if whitespace exists in the string
        ArgumentContainsWhitespace(argument);

    internal static bool IsSurroundedWithQuotes(string argument) =>
        argument.StartsWith("\"", StringComparison.Ordinal) && argument.EndsWith("\"", StringComparison.Ordinal);

    internal static bool ArgumentContainsWhitespace(string argument) =>
        argument.Contains(" ") || argument.Contains("\t") || argument.Contains("\n");

    // Taken from Roslyn's CommandLineParser.
    internal static ReadOnlyMemory<char> RemoveQuotesAndSlashes(ReadOnlyMemory<char> argMemory)
    {
        if (RemoveFastPath(argMemory) is { } m)
        {
            return m;
        }

        var builder = new StringBuilder();
        var arg = argMemory.Span;
        var i = 0;
        while (i < arg.Length)
        {
            var cur = arg[i];
            switch (cur)
            {
                case '\\':
                    ProcessSlashes(builder, arg, ref i);
                    break;
                case '"':
                    // Intentionally dropping quotes that don't have explicit escaping.
                    i++;
                    break;
                default:
                    builder.Append(cur);
                    i++;
                    break;
            }
        }

        return builder.ToString().AsMemory();

        static void ProcessSlashes(StringBuilder builder, ReadOnlySpan<char> arg, ref int i)
        {
            Debug.Assert(i < arg.Length);

            var slashCount = 0;
            while (i < arg.Length && arg[i] == '\\')
            {
                slashCount++;
                i++;
            }

            if (i < arg.Length && arg[i] == '"')
            {
                // Before a quote slashes are interpretted as escape sequences for other slashes so
                // output one for every two.
                while (slashCount >= 2)
                {
                    builder.Append('\\');
                    slashCount -= 2;
                }

                Debug.Assert(slashCount >= 0);

                // If there is an odd number of slashes then the quote is escaped and hence a part
                // of the output.  Otherwise it is a normal quote and can be ignored. 
                if (slashCount == 1)
                {
                    // The quote is escaped so eat it.
                    builder.Append('"');
                }

                i++;
            }
            else
            {
                // Slashes that aren't followed by quotes are simply slashes.
                while (slashCount > 0)
                {
                    builder.Append('\\');
                    slashCount--;
                }
            }
        }

        // Avoids allocation when an arg has quotes at the start and end of the string but no where else.
        static ReadOnlyMemory<char>? RemoveFastPath(ReadOnlyMemory<char> arg)
        {
            int start = 0;
            int end = arg.Length;
            var span = arg.Span;

            while (end > 0 && span[end - 1] == '"')
            {
                end--;
            }

            while (start < end && span[start] == '"')
            {
                start++;
            }

            for (int i = start; i < end; i++)
            {
                if (span[i] == '"')
                {
                    return null;
                }
            }

            return arg.Slice(start, end - start);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

#if NET
public static class ChmodHelper
{
    public static UnixFileMode GetArguments(ReadOnlySpan<char> argument, string path)
    {
        if (argument.Length == 0)
        {
            throw CreateParseException(argument);
        }

        UnixFileMode namedMode = 0;
        bool sawToken = false;
        bool allTokensValid = true;

        foreach (Range range in argument.Split('|'))
        {
            ReadOnlySpan<char> token = argument[range];
            if (token.Length == 0)
            {
                continue;
            }

            sawToken = true;

            if (int.TryParse(token, out _))
            {
                allTokensValid = false;
                break;
            }

            if (!Enum.TryParse(token, ignoreCase: true, out UnixFileMode flag))
            {
                allTokensValid = false;
                break;
            }

            namedMode |= flag;
        }

        if (sawToken && allTokensValid)
        {
            return namedMode;
        }

        UnixFileMode existingMode = File.GetUnixFileMode(path);
        return Parse(argument, existing: existingMode, isDir: Directory.Exists(path));
    }

    // compatible with POSIX parsing, e.g. https://github.com/landley/toybox/blob/c9c0d42aae50b80cda27bd6131f315e57513be47/lib/lib.c#L950
    private static UnixFileMode Parse(ReadOnlySpan<char> argument, UnixFileMode existing = 0, UnixFileMode umask = 0, bool isDir = false)
    {
        // Octal parsing: allow up to 17777
        bool isOctal = true;
        for (int idx = 0; idx < argument.Length; idx++)
        {
            if (argument[idx] < '0' || argument[idx] > '7')
            {
                isOctal = false;
                break;
            }
        }

        if (isOctal && argument.Length > 0)
        {
            int val = 0;
            for (int idx = 0; idx < argument.Length; idx++)
                val = (val * 8) + (argument[idx] - '0');
            if (val > 0x1FFF) throw CreateParseException(argument);
            return (UnixFileMode)val;
        }

        UnixFileMode mode = existing;
        int i = 0;

        while (i < argument.Length)
        {
            // Skip whitespace/commas
            while (i < argument.Length && (argument[i] == ',' || char.IsWhiteSpace(argument[i]))) i++;
            if (i >= argument.Length) break;

            // Start of a clause
            bool u = false, g = false, o = false;
            bool classesSpecified = false;
            bool clauseStart = true;

            while (i < argument.Length && argument[i] != ',')
            {
                // Parse who only if at clause start
                if (clauseStart)
                {
                    while (i < argument.Length)
                    {
                        char c = argument[i];
                        if (c == 'u') { u = true; classesSpecified = true; i++; }
                        else if (c == 'g') { g = true; classesSpecified = true; i++; }
                        else if (c == 'o') { o = true; classesSpecified = true; i++; }
                        else if (c == 'a') { u = g = o = true; classesSpecified = true; i++; }
                        else break;
                    }
                    clauseStart = false;
                }

                // Default to all classes if none specified
                bool applyToAllClasses = !classesSpecified;

                if (i >= argument.Length || "+-=".IndexOf(argument[i]) == -1)
                    throw CreateParseException(argument);
                char op = argument[i++];

                UnixFileMode permMask = 0;
                bool specialT = false;

                while (i < argument.Length && argument[i] != ',' && "+-=".IndexOf(argument[i]) == -1)
                {
                    char p = argument[i++];
                    switch (p)
                    {
                        case 'r':
                            if (u || applyToAllClasses) permMask |= UnixFileMode.UserRead;
                            if (g || applyToAllClasses) permMask |= UnixFileMode.GroupRead;
                            if (o || applyToAllClasses) permMask |= UnixFileMode.OtherRead;
                            break;
                        case 'w':
                            if (u || applyToAllClasses) permMask |= UnixFileMode.UserWrite;
                            if (g || applyToAllClasses) permMask |= UnixFileMode.GroupWrite;
                            if (o || applyToAllClasses) permMask |= UnixFileMode.OtherWrite;
                            break;
                        case 'x':
                            if (u || applyToAllClasses) permMask |= UnixFileMode.UserExecute;
                            if (g || applyToAllClasses) permMask |= UnixFileMode.GroupExecute;
                            if (o || applyToAllClasses) permMask |= UnixFileMode.OtherExecute;
                            break;
                        case 'X':
                            if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0 || isDir)
                            {
                                if (u || applyToAllClasses) permMask |= UnixFileMode.UserExecute;
                                if (g || applyToAllClasses) permMask |= UnixFileMode.GroupExecute;
                                if (o || applyToAllClasses) permMask |= UnixFileMode.OtherExecute;
                            }

                            break;
                        case 's':
                            if (u || applyToAllClasses) permMask |= UnixFileMode.SetUser;
                            if (g || applyToAllClasses) permMask |= UnixFileMode.SetGroup;
                            break;
                        case 't':
                            specialT = true;
                            break;
                        case 'u':
                        {
                            var bits = mode & (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                            if (u || applyToAllClasses) permMask |= bits;
                            if (g || applyToAllClasses) permMask |= (UnixFileMode)((int)bits >> 3);
                            if (o || applyToAllClasses) permMask |= (UnixFileMode)((int)bits >> 6);
                            break;
                        }
                        case 'g':
                        {
                            var bits = mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute);
                            if (u || applyToAllClasses) permMask |= (UnixFileMode)((int)bits << 3);
                            if (g || applyToAllClasses) permMask |= bits;
                            if (o || applyToAllClasses) permMask |= (UnixFileMode)((int)bits >> 3);
                            break;
                        }
                        case 'o':
                        {
                            var bits = mode & (UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
                            if (u || applyToAllClasses) permMask |= (UnixFileMode)((int)bits << 6);
                            if (g || applyToAllClasses) permMask |= (UnixFileMode)((int)bits << 3);
                            if (o || applyToAllClasses) permMask |= bits;
                            break;
                        }
                        default:
                            break;
                    }
                }

                if (specialT) permMask |= UnixFileMode.StickyBit;
                if (applyToAllClasses && (op == '+' || op == '=')) permMask &= ~umask;

                if (op == '=')
                {
                    // class mask covers all relevant classes
                    UnixFileMode classMask = 0;
                    if (u || applyToAllClasses) classMask |= UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.SetUser;
                    if (g || applyToAllClasses) classMask |= UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.SetGroup;
                    if (o || applyToAllClasses) classMask |= UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                    if ((permMask & UnixFileMode.StickyBit) == 0) mode &= ~UnixFileMode.StickyBit;
                    mode = (mode & ~classMask) | permMask;
                }
                else if (op == '+') mode |= permMask;
                else if (op == '-') mode &= ~permMask;
            }

            i++; // skip comma
        }

        return mode;
    }

    private static FilePermissionSettingException CreateParseException(ReadOnlySpan<char> argument)
        => new(string.Format(LocalizableStrings.MalformedText, argument.ToString()));
}
#endif

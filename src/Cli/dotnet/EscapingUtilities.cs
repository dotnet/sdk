// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools
{
    internal static class EscapingUtilities
    {
        private static Dictionary<string, string> s_unescapedToEscapedStrings = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly char[] s_charsToEscape = { '%', '*', '?', '@', '$', '(', ')', ';', '\'' };

        internal static string UnescapeAll(string escapedString, bool trim = false)
        {
            // If the string doesn't contain anything, then by definition it doesn't
            // need unescaping.
            if (String.IsNullOrEmpty(escapedString))
            {
                return escapedString;
            }

            // If there are no percent signs, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            int indexOfPercent = escapedString.IndexOf('%');
            if (indexOfPercent == -1)
            {
                return trim ? escapedString.Trim() : escapedString;
            }

            // This is where we're going to build up the final string to return to the caller.
            StringBuilder unescapedString = StringBuilderCache.Acquire(escapedString.Length);

            int currentPosition = 0;
            int escapedStringLength = escapedString.Length;
            if (trim)
            {
                while (currentPosition < escapedString.Length && Char.IsWhiteSpace(escapedString[currentPosition]))
                {
                    currentPosition++;
                }
                if (currentPosition == escapedString.Length)
                {
                    return String.Empty;
                }
                while (Char.IsWhiteSpace(escapedString[escapedStringLength - 1]))
                {
                    escapedStringLength--;
                }
            }

            // Loop until there are no more percent signs in the input string.
            while (indexOfPercent != -1)
            {
                // There must be two hex characters following the percent sign
                // for us to even consider doing anything with this.
                if (
                        (indexOfPercent <= (escapedStringLength - 3)) &&
                        TryDecodeHexDigit(escapedString[indexOfPercent + 1], out int digit1) &&
                        TryDecodeHexDigit(escapedString[indexOfPercent + 2], out int digit2))
                {
                    // First copy all the characters up to the current percent sign into
                    // the destination.
                    unescapedString.Append(escapedString, currentPosition, indexOfPercent - currentPosition);

                    // Convert the %XX to an actual real character.
                    char unescapedCharacter = (char)((digit1 << 4) + digit2);

                    // if the unescaped character is not on the exception list, append it
                    unescapedString.Append(unescapedCharacter);

                    // Advance the current pointer to reflect the fact that the destination string
                    // is up to date with everything up to and including this escape code we just found.
                    currentPosition = indexOfPercent + 3;
                }

                // Find the next percent sign.
                indexOfPercent = escapedString.IndexOf('%', indexOfPercent + 1);
            }

            // Okay, there are no more percent signs in the input string, so just copy the remaining
            // characters into the destination.
            unescapedString.Append(escapedString, currentPosition, escapedStringLength - currentPosition);

            return StringBuilderCache.GetStringAndRelease(unescapedString);
        }

        internal static string Escape(string unescapedString)
        {
            return EscapeWithOptionalCaching(unescapedString, cache: false);
        }

        private static bool TryDecodeHexDigit(char character, out int value)
        {
            if (character >= '0' && character <= '9')
            {
                value = character - '0';
                return true;
            }
            if (character >= 'A' && character <= 'F')
            {
                value = character - 'A' + 10;
                return true;
            }
            if (character >= 'a' && character <= 'f')
            {
                value = character - 'a' + 10;
                return true;
            }
            value = default;
            return false;
        }

        private static string EscapeWithOptionalCaching(string unescapedString, bool cache)
        {
            // If there are no special chars, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            if (String.IsNullOrEmpty(unescapedString) || !ContainsReservedCharacters(unescapedString))
            {
                return unescapedString;
            }

            // next, if we're caching, check to see if it's already there.
            if (cache)
            {
                lock (s_unescapedToEscapedStrings)
                {
                    string cachedEscapedString;
                    if (s_unescapedToEscapedStrings.TryGetValue(unescapedString, out cachedEscapedString))
                    {
                        return cachedEscapedString;
                    }
                }
            }

            // This is where we're going to build up the final string to return to the caller.
            StringBuilder escapedStringBuilder = StringBuilderCache.Acquire(unescapedString.Length * 2);

            AppendEscapedString(escapedStringBuilder, unescapedString);

            if (!cache)
            {
                return StringBuilderCache.GetStringAndRelease(escapedStringBuilder);
            }

            //string escapedString = Strings.WeakIntern(escapedStringBuilder.ToString());
            string escapedString = escapedStringBuilder.ToString();
            StringBuilderCache.Release(escapedStringBuilder);

            lock (s_unescapedToEscapedStrings)
            {
                s_unescapedToEscapedStrings[unescapedString] = escapedString;
            }

            return escapedString;
        }

        private static bool ContainsReservedCharacters(string unescapedString)
        {
            return -1 != unescapedString.IndexOfAny(s_charsToEscape);
        }

        private static void AppendEscapedString(StringBuilder sb, string unescapedString)
        {
            // Replace each unescaped special character with an escape sequence one
            for (int idx = 0; ;)
            {
                int nextIdx = unescapedString.IndexOfAny(s_charsToEscape, idx);
                if (nextIdx == -1)
                {
                    sb.Append(unescapedString, idx, unescapedString.Length - idx);
                    break;
                }

                sb.Append(unescapedString, idx, nextIdx - idx);
                AppendEscapedChar(sb, unescapedString[nextIdx]);
                idx = nextIdx + 1;
            }
        }

        private static void AppendEscapedChar(StringBuilder sb, char ch)
        {
            // Append the escaped version which is a percent sign followed by two hexadecimal digits
            sb.Append('%');
            sb.Append(HexDigitChar(ch / 0x10));
            sb.Append(HexDigitChar(ch & 0x0F));
        }

        private static char HexDigitChar(int x)
        {
            return (char)(x + (x < 10 ? '0' : ('a' - 10)));
        }
    }
}

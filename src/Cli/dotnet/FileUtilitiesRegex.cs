// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Tools
{
    internal static class FileUtilitiesRegex
    {
        private const char _backSlash = '\\';
        private const char _forwardSlash = '/';

        internal static bool IsDrivePattern(string pattern)
        {
            // Format must be two characters long: "<drive letter>:"
            return pattern.Length == 2 &&
                StartsWithDrivePattern(pattern);
        }

        internal static bool StartsWithDrivePattern(string pattern)
        {
            // Format dictates a length of at least 2,
            // first character must be a letter,
            // second character must be a ":"
            return pattern.Length >= 2 &&
                ((pattern[0] >= 'A' && pattern[0] <= 'Z') || (pattern[0] >= 'a' && pattern[0] <= 'z')) &&
                pattern[1] == ':';
        }

        internal static bool IsUncPattern(string pattern)
        {
            // Return value == pattern.length means:
            //  meets minimum unc requirements
            //  pattern does not end in a '/' or '\'
            //  if a subfolder were found the value returned would be length up to that subfolder, therefore no subfolder exists
            return StartsWithUncPatternMatchLength(pattern) == pattern.Length;
        }

        internal static int StartsWithUncPatternMatchLength(string pattern)
        {
            if (!MeetsUncPatternMinimumRequirements(pattern))
            {
                return -1;
            }

            bool prevCharWasSlash = true;
            bool hasShare = false;

            for (int i = 2; i < pattern.Length; i++)
            {
                // Real UNC paths should only contain backslashes. However, the previous
                // regex pattern accepted both so functionality will be retained.
                if (pattern[i] == _backSlash ||
                    pattern[i] == _forwardSlash)
                {
                    if (prevCharWasSlash)
                    {
                        // We get here in the case of an extra slash.
                        return -1;
                    }
                    else if (hasShare)
                    {
                        return i;
                    }

                    hasShare = true;
                    prevCharWasSlash = true;
                }
                else
                {
                    prevCharWasSlash = false;
                }
            }

            if (!hasShare)
            {
                // no subfolder means no unc pattern. string is something like "\\abc" in this case
                return -1;
            }

            return pattern.Length;
        }

#if !NET35
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool MeetsUncPatternMinimumRequirements(string pattern)
        {
            return pattern.Length >= 5 &&
                (pattern[0] == _backSlash ||
                pattern[0] == _forwardSlash) &&
                (pattern[1] == _backSlash ||
                pattern[1] == _forwardSlash);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    public static class FileUtilities
    {
        public const string CR = "\r";
        public const string CRLF = "\r\n";
        public const string LF = "\n";

        public static string DetectNewLineChars(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Contains(CRLF))
            {
                return CRLF;
            }
            else if (source.Contains(LF))
            {
                return LF;
            }
            else if (source.Contains(CR))
            {
                return CR;
            }
            else
            {
                throw new ArgumentException("Unsupported new line characters", nameof(source));
            }
        }

        public static string NormalizeNewLineChars(string source, string newLineChars)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            source = source.Replace(CRLF, LF).Replace(CR, LF);
            if (newLineChars == CRLF)
            {
                source = source.Replace(LF, CRLF);
            }
            else if (newLineChars == CR)
            {
                source = source.Replace(LF, CR);
            }
            else if (newLineChars != LF)
            {
                throw new ArgumentException("Unsupported new line characters", nameof(newLineChars));
            }

            // Ensure trailing new line on linux.
            if (newLineChars == LF && !source.EndsWith(newLineChars))
            {
                source += newLineChars;
            }

            return source;
        }
    }
}

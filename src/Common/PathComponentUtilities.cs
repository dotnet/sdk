// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet
{
    internal static class PathComponentUtilities
    {
        public static bool IsValidPathComponent(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value != "."
                && value != ".."
                && !Path.IsPathRooted(value)
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && (Path.DirectorySeparatorChar != '\\'
                    || (!value.EndsWith(".", StringComparison.Ordinal)
                        && !value.EndsWith(" ", StringComparison.Ordinal)));
        }
    }
}

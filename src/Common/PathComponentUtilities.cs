// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet;

static class PathComponentUtilities
{
    /// <summary>
    /// Determines whether a value can be used as a single file or directory name on the
    /// current operating system.
    /// </summary>
    public static bool IsValidPathComponent(string value)
        => !string.IsNullOrWhiteSpace(value) &&
            value != "." &&
            value != ".." &&
            !Path.IsPathRooted(value) &&
            value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            (Path.DirectorySeparatorChar != '\\' ||
                (!value.EndsWith(".", StringComparison.Ordinal) &&
                 !value.EndsWith(" ", StringComparison.Ordinal)));
}

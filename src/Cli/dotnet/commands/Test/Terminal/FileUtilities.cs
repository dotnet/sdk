// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal static class FileUtilities
{
    internal static readonly StringComparison PathComparison = GetIsFileSystemCaseSensitive() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    internal static readonly StringComparer PathComparer = GetIsFileSystemCaseSensitive() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Determines whether the file system is case sensitive.
    /// Copied from https://github.com/dotnet/runtime/blob/73ba11f3015216b39cb866d9fb7d3d25e93489f2/src/libraries/Common/src/System/IO/PathInternal.CaseSensitivity.cs#L41-L59 .
    /// </summary>
    public static bool GetIsFileSystemCaseSensitive()
    {
        try
        {
            string pathWithUpperCase = Path.Combine(Path.GetTempPath(), "CASESENSITIVETEST" + Guid.NewGuid().ToString("N"));
            using (new FileStream(pathWithUpperCase, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 0x1000, FileOptions.DeleteOnClose))
            {
                string lowerCased = pathWithUpperCase.ToLowerInvariant();
                return !File.Exists(lowerCased);
            }
        }
        catch (Exception exc)
        {
            // In case something goes terribly wrong, we don't want to fail just because
            // of a casing test, so we assume case-insensitive-but-preserving.
            Debug.Fail("Casing test failed: " + exc);
            return false;
        }
    }
}

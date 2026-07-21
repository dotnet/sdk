// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.NativeWrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Resolves the real (symlink-followed) directory that contains an executable. Centralizes the
/// <c>GetFullPath</c> → <c>ResolveRealPath</c> → directory logic so every PATH-resolution caller
/// agrees on whether a found executable maps to a managed install, even when the executable is
/// reached through a symlink (for example a distro/Homebrew <c>dotnet</c> that links into the
/// dotnetup-managed install directory).
/// </summary>
internal static class ExecutablePathResolver
{
    /// <summary>
    /// Returns the real directory containing <paramref name="executablePath"/> with symlinks
    /// resolved, or <c>null</c> when the path is null or empty (for example, when the command was
    /// not found on PATH). <see cref="FileInterop.ResolveRealPath"/> is a no-op on Windows, so the
    /// result there is the normalized full-path directory.
    /// </summary>
    public static string? ResolveRealDirectory(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(executablePath);
        string resolvedPath = FileInterop.ResolveRealPath(fullPath) ?? fullPath;
        return Path.GetDirectoryName(resolvedPath);
    }
}

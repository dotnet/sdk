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
        string? resolvedPath = ResolveRealPath(executablePath);
        return resolvedPath is null ? null : Path.GetDirectoryName(resolvedPath);
    }

    /// <summary>
    /// Returns <paramref name="path"/> itself with symlinks resolved (unlike
    /// <see cref="ResolveRealDirectory"/>, which resolves the containing directory of an
    /// executable). Use this to canonicalize a directory that may be reached through a symlinked
    /// parent, such as a dotnet install root under a symlinked <c>LocalApplicationData</c> /
    /// <c>XDG_DATA_HOME</c>. Returns <c>null</c> when the path is null or empty.
    /// <see cref="FileInterop.ResolveRealPath"/> is a no-op on Windows, and on Unix returns
    /// <c>null</c> for a path that does not yet exist; in both cases the normalized full path is
    /// returned so callers still get a usable value.
    /// </summary>
    public static string? ResolveRealPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(path);
        return FileInterop.ResolveRealPath(fullPath) ?? fullPath;
    }
}

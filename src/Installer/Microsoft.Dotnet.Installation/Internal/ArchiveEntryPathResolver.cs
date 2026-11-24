// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

internal static class ArchiveEntryPathResolver
{
    public static string Normalize(string entryName)
        => entryName.StartsWith("./", StringComparison.Ordinal)
            ? entryName.Substring(2)
            : entryName;

    public static string ResolveDestination(string entryName, string targetDirectory, MuxerHandler? muxerHandler)
    {
        string normalizedName = Normalize(entryName);
        if (muxerHandler is not null && normalizedName == MuxerHandler.MuxerEntryName)
        {
            muxerHandler.MuxerWasExtracted = true;
            return muxerHandler.TempMuxerPath;
        }

        string targetPath = Path.GetFullPath(targetDirectory);
        string destinationPath = Path.GetFullPath(Path.Combine(targetPath, normalizedName));
        string targetPrefix = targetPath + Path.DirectorySeparatorChar;
        if (!destinationPath.StartsWith(targetPrefix, DotnetupUtilities.PathComparison) &&
            !string.Equals(destinationPath, targetPath, DotnetupUtilities.PathComparison))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ArchiveCorrupted,
                $"Archive entry '{entryName}' would extract outside target directory.");
        }

        return destinationPath;
    }
}

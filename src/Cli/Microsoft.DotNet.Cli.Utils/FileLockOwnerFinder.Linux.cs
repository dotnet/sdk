// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Globalization;

namespace Microsoft.DotNet.Cli.Utils;

internal static partial class FileLockOwnerFinder
{
    private static FileLockOwner? TryGetLinuxFileLockOwner(string filePath)
    {
        string targetPath = ResolvePath(filePath);

        foreach (int processId in GetExclusiveFlockOwnerProcessIds())
        {
            if (TryFindOpenFile(processId, targetPath) &&
                TryGetLinuxProcessName(processId) is string processName)
            {
                return new FileLockOwner(processId, processName);
            }
        }

        return null;
    }

    private static IEnumerable<int> GetExclusiveFlockOwnerProcessIds()
    {
        string[] lockEntries;
        try
        {
            lockEntries = File.ReadAllLines("/proc/locks");
        }
        catch (Exception)
        {
            yield break;
        }

        var processIds = new HashSet<int>();
        foreach (string lockEntry in lockEntries)
        {
            if (TryParseExclusiveFlockOwnerProcessId(lockEntry, out int processId) &&
                processId > 0 &&
                processIds.Add(processId))
            {
                yield return processId;
            }
        }
    }

    private static bool TryParseExclusiveFlockOwnerProcessId(
        ReadOnlySpan<char> lockEntry,
        out int processId)
    {
        processId = 0;

        _ = ReadNextField(ref lockEntry);
        return ReadNextField(ref lockEntry).SequenceEqual("FLOCK") &&
            ReadNextField(ref lockEntry).SequenceEqual("ADVISORY") &&
            ReadNextField(ref lockEntry).SequenceEqual("WRITE") &&
            int.TryParse(
                ReadNextField(ref lockEntry),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out processId);
    }

    private static ReadOnlySpan<char> ReadNextField(ref ReadOnlySpan<char> remaining)
    {
        int fieldStart = 0;
        while (fieldStart < remaining.Length && char.IsWhiteSpace(remaining[fieldStart]))
        {
            fieldStart++;
        }

        remaining = remaining[fieldStart..];
        int fieldLength = 0;
        while (fieldLength < remaining.Length && !char.IsWhiteSpace(remaining[fieldLength]))
        {
            fieldLength++;
        }

        ReadOnlySpan<char> field = remaining[..fieldLength];
        remaining = remaining[fieldLength..];
        return field;
    }

    private static bool TryFindOpenFile(int processId, string targetPath)
    {
        string[] fileDescriptors;
        try
        {
            fileDescriptors = Directory.GetFileSystemEntries($"/proc/{processId}/fd");
        }
        catch (Exception)
        {
            return false;
        }

        foreach (string fileDescriptor in fileDescriptors)
        {
            try
            {
                FileSystemInfo? linkTarget = new FileInfo(fileDescriptor).ResolveLinkTarget(returnFinalTarget: true);
                if (linkTarget is not null &&
                    string.Equals(linkTarget.FullName, targetPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // The process can close a descriptor while it is being inspected.
            }
        }

        return false;
    }

    private static string? TryGetLinuxProcessName(int processId)
    {
        try
        {
            FileSystemInfo? executable = new FileInfo($"/proc/{processId}/exe").ResolveLinkTarget(returnFinalTarget: true);
            string? executableName = Path.GetFileName(executable?.FullName);
            if (!string.IsNullOrWhiteSpace(executableName))
            {
                string sanitizedName = SanitizeProcessName(executableName);
                if (!string.IsNullOrWhiteSpace(sanitizedName))
                {
                    return sanitizedName;
                }
            }
        }
        catch (Exception)
        {
            // Fall back to the process comm value.
        }

        try
        {
            string processName = File.ReadAllText($"/proc/{processId}/comm").Trim();
            string sanitizedName = SanitizeProcessName(processName);
            return string.IsNullOrWhiteSpace(sanitizedName) ? null : sanitizedName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ResolvePath(string filePath)
    {
        var fileInfo = new FileInfo(Path.GetFullPath(filePath));
        return fileInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? fileInfo.FullName;
    }
}

#endif

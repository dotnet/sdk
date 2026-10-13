// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Globalization;

namespace Microsoft.DotNet.Cli.Utils;

internal static partial class FileLockOwnerFinder
{
    /// <summary>
    /// Correlates Linux advisory lock owners with the target path so contention diagnostics report the correct process.
    /// </summary>
    /// <remarks>
    /// <c>/proc/locks</c> supplies candidate PIDs, which are verified against <c>/proc/&lt;pid&gt;/fd</c>.
    /// Permission restrictions and process lifetime races are treated as an unknown owner.
    /// </remarks>
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

    /// <summary>
    /// Enumerates candidate PIDs that hold the kind of advisory lock used by <see cref="FileShare.None"/>.
    /// </summary>
    /// <remarks>
    /// .NET implements <see cref="FileShare.None"/> with an exclusive <c>flock</c> on Linux. Duplicate PIDs are
    /// removed because a process can own more than one such lock.
    /// </remarks>
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

    /// <summary>
    /// Parses the owner PID from a Linux exclusive <c>flock</c> record without allocating strings for its fields.
    /// </summary>
    /// <remarks>Only <c>FLOCK ADVISORY WRITE</c> records are relevant to the lock used by this feature.</remarks>
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

    /// <summary>
    /// Consumes the next whitespace-delimited field from a <c>/proc/locks</c> record without allocating.
    /// </summary>
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

    /// <summary>
    /// Confirms that a candidate lock-owning process has the target file open.
    /// </summary>
    /// <remarks>Descriptors can disappear during inspection, so individual lookup failures are ignored.</remarks>
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

    /// <summary>
    /// Gets a stable process label for the contention message.
    /// </summary>
    /// <remarks>
    /// The executable symlink provides the preferred name; <c>/proc/&lt;pid&gt;/comm</c> is used when it is
    /// unavailable.
    /// </remarks>
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

    /// <summary>
    /// Resolves the target path before comparing it with process descriptor symlink targets.
    /// </summary>
    /// <remarks>Resolution is best-effort and falls back to the absolute path when the final entry is not a symlink.</remarks>
    private static string ResolvePath(string filePath)
    {
        var fileInfo = new FileInfo(Path.GetFullPath(filePath));
        return fileInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? fileInfo.FullName;
    }
}

#endif

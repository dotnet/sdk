// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Finds the stale files and the directories owned by the workload manifest layout, and rejects
/// mappings whose destinations are outside that ownership.
/// </summary>
/// <remarks>
/// Only <c>LayoutManifests</c> writes under the <c>sdk-manifests</c> staging root, so ownership
/// comes from the directory shape rather than from cached inventories. The layout owns every file
/// under <c>&lt;feature-band&gt;/&lt;manifest-ID&gt;/&lt;version&gt;/</c>, except that a
/// <c>workloadsets</c> version owns only its <c>baseline.workloadset.json</c>.
/// </remarks>
public sealed class GetWorkloadManifestLayout : Task
{
    private const string WorkloadSetsDirectoryName = "workloadsets";
    private const string BaselineFileName = "baseline.workloadset.json";

    /// <summary>
    /// Gets or sets the fully qualified <c>sdk-manifests</c> staging directory. It need not exist yet.
    /// </summary>
    [Required]
    public string LayoutRoot { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the fully qualified source files to lay out. Each has a fully qualified
    /// <c>DestinationPath</c> that must be an owned path under <see cref="LayoutRoot"/>.
    /// </summary>
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>
    /// Gets the owned files that the current mappings don't produce. On a case-insensitive
    /// comparison, this includes files whose name, or whose version directory's name, differs from
    /// the mapped destination only in case, because copying over them would keep the old name.
    /// </summary>
    [Output]
    public ITaskItem[] StaleOutputs { get; private set; } = [];

    /// <summary>
    /// Gets the owned directories that currently exist, excluding <see cref="LayoutRoot"/>.
    /// </summary>
    [Output]
    public ITaskItem[] OwnedDirectories { get; private set; } = [];

    /// <summary>
    /// Gets the owned directories that currently have no entries.
    /// </summary>
    [Output]
    public ITaskItem[] EmptyDirectories { get; private set; } = [];

    public override bool Execute()
    {
        try
        {
            if (!IncrementalLayoutState.TryGetFullPath(Log, LayoutRoot, nameof(LayoutRoot), out string root))
            {
                return false;
            }

            Dictionary<string, string> destinations = ValidateMappings(root);

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            var files = new List<string>();
            var directories = new List<string>();

            if (Directory.Exists(root))
            {
                FindOwnedPaths(root, files, directories);
            }

            StaleOutputs = files
                .Where(path => !destinations.TryGetValue(path, out string? destination) || !HasSameOwnedCasing(root, path, destination))
                .Select(path => (ITaskItem)new TaskItem(path))
                .ToArray();
            OwnedDirectories = directories.Select(path => (ITaskItem)new TaskItem(path)).ToArray();
            EmptyDirectories = directories
                .Where(path => !Directory.EnumerateFileSystemEntries(path).Any())
                .Select(path => (ITaskItem)new TaskItem(path))
                .ToArray();
            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }

    private Dictionary<string, string> ValidateMappings(string root)
    {
        var destinations = new Dictionary<string, string>(IncrementalLayoutState.PathComparer);

        foreach (ITaskItem source in SourceFiles)
        {
            if (!IncrementalLayoutState.TryGetFullPath(Log, source.ItemSpec, nameof(SourceFiles), out string sourcePath)
                || !IncrementalLayoutState.TryGetFullPath(Log, source.GetMetadata("DestinationPath"), "DestinationPath", out string destination))
            {
                continue;
            }

            // A source inside the layout could be deleted as a stale output before it is copied.
            bool isValid = IsOwnedPath(root, destination) && !IsWithin(root, sourcePath);

            if (!isValid)
            {
                Log.LogError($"Invalid workload manifest layout mapping '{sourcePath}' to '{destination}' under '{root}'.");
            }

            destinations.TryAdd(destination, destination);
        }

        return destinations;
    }

    // Compares case only from the version directory down (just the file name for workload sets).
    // Those directories hold nothing but owned files, so deleting mismatched files empties them and
    // pruning lets the copy recreate them with the new name. Higher directories can hold unowned
    // files, so they could never be renamed and the layout would rerun on every build.
    private static bool HasSameOwnedCasing(string root, string existing, string destination)
    {
        string[] existingParts = Path.GetRelativePath(root, existing).Split(Path.DirectorySeparatorChar);
        string[] destinationParts = Path.GetRelativePath(root, destination).Split(Path.DirectorySeparatorChar);
        int ownedStart = IsWorkloadSets(existingParts[1]) ? 3 : 2;

        return existingParts.AsSpan(ownedStart).SequenceEqual(destinationParts.AsSpan(ownedStart));
    }

    private static void FindOwnedPaths(string root, List<string> files, List<string> directories)
    {
        foreach (string band in Directory.EnumerateDirectories(root))
        {
            if (!IsFeatureBand(Path.GetFileName(band)))
            {
                continue;
            }

            directories.Add(band);

            foreach (string manifest in Directory.EnumerateDirectories(band))
            {
                directories.Add(manifest);
                bool isWorkloadSets = IsWorkloadSets(Path.GetFileName(manifest));

                foreach (string version in Directory.EnumerateDirectories(manifest))
                {
                    if (!IsVersion(Path.GetFileName(version)))
                    {
                        continue;
                    }

                    directories.Add(version);

                    if (isWorkloadSets)
                    {
                        string baseline = Path.Combine(version, BaselineFileName);

                        if (File.Exists(baseline))
                        {
                            files.Add(baseline);
                        }
                    }
                    else
                    {
                        files.AddRange(Directory.EnumerateFiles(version, "*", SearchOption.AllDirectories));
                        directories.AddRange(Directory.EnumerateDirectories(version, "*", SearchOption.AllDirectories));
                    }
                }
            }
        }
    }

    private static bool IsOwnedPath(string root, string path)
    {
        if (!IsWithin(root, path))
        {
            return false;
        }

        string[] parts = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar);

        if (parts.Length < 4 || !IsFeatureBand(parts[0]) || !IsVersion(parts[2]))
        {
            return false;
        }

        return !IsWorkloadSets(parts[1])
            || (parts.Length == 4 && IncrementalLayoutState.PathComparer.Equals(parts[3], BaselineFileName));
    }

    private static bool IsWithin(string root, string path) =>
        path.StartsWith(
            Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    // SDK feature bands have a patch number that is a multiple of 100, such as 11.0.100 or 11.0.200-preview.2.
    private static bool IsFeatureBand(string value) =>
        Regex.IsMatch(value, @"\A[0-9]+\.[0-9]+\.[0-9]*00(?:-.+)?\z", RegexOptions.CultureInvariant);

    // Manifest and workload-set versions are NuGet versions with three or four numeric parts.
    private static bool IsVersion(string value) =>
        Regex.IsMatch(value, @"\A[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:[-+].+)?\z", RegexOptions.CultureInvariant);

    private static bool IsWorkloadSets(string value) =>
        string.Equals(value, WorkloadSetsDirectoryName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Removes directories that are empty, deepest first, without deleting any contents.
/// </summary>
public sealed class PruneEmptyLayoutDirectories : Task
{
    /// <summary>
    /// Gets or sets the directories to remove if they are empty. Missing and nonempty directories
    /// are left alone.
    /// </summary>
    public ITaskItem[] Directories { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            IEnumerable<string> deepestFirst = Directories
                .Select(directory => directory.GetMetadata("FullPath"))
                .OrderByDescending(path => path.Length);

            foreach (string path in deepestFirst)
            {
                bool isEmpty = Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any();

                if (isEmpty)
                {
                    Directory.Delete(path, recursive: false);
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Discovers owned workload manifest payloads and prepares stable inventories for incremental layout.
/// </summary>
/// <remarks>
/// Ownership comes from recognized feature-band, manifest-ID, and version directories, not cached
/// inventories. All files beneath ordinary manifest versions are owned; workload-set versions own
/// only their directly nested <c>baseline.workloadset.json</c>. Discovery does not modify the layout.
/// Callers must use a dedicated staging root and run <see cref="PrepareIncrementalLayout"/> before
/// mutation to validate source existence and destination uniqueness.
/// </remarks>
public sealed class GetWorkloadManifestLayout : Task
{
    /// <summary>
    /// Gets or sets the fully qualified staging directory containing SDK feature-band directories.
    /// The root need not exist yet.
    /// </summary>
    [Required]
    public string LayoutRoot { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets fully qualified source files, each with fully qualified <c>DestinationPath</c>
    /// metadata naming an owned payload path. Sources must be outside <see cref="LayoutRoot"/>.
    /// </summary>
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>
    /// Gets copies of the source items with normalized source and destination paths, sorted by
    /// destination. Other source metadata is preserved.
    /// </summary>
    [Output]
    public ITaskItem[] SourceFilesWithDestinations { get; private set; } = [];

    /// <summary>
    /// Gets existing owned files, including payloads from retired feature bands, manifests,
    /// and versions that are absent from the current mapping.
    /// </summary>
    [Output]
    public ITaskItem[] ExistingOutputs { get; private set; } = [];

    /// <summary>
    /// Gets existing owned files absent from the expected destinations. Path comparison ignores
    /// case on Windows and is ordinal on other platforms.
    /// </summary>
    [Output]
    public ITaskItem[] StaleOutputs { get; private set; } = [];

    /// <summary>
    /// Gets recognized layout containers and discovered payload directories eligible for
    /// empty-only pruning. The layout root is excluded.
    /// </summary>
    [Output]
    public ITaskItem[] OwnedDirectories { get; private set; } = [];

    /// <summary>
    /// Gets owned directories that are empty during discovery, allowing callers to invalidate
    /// an otherwise up-to-date layout. Emptiness must be checked again before deletion.
    /// </summary>
    [Output]
    public ITaskItem[] EmptyDirectories { get; private set; } = [];

    /// <summary>
    /// Gets serialized root and source-to-destination mappings for a write-if-different input
    /// inventory. Paths use canonical casing on Windows, and mappings are sorted by destination.
    /// </summary>
    [Output]
    public ITaskItem[] InputManifestLines { get; private set; } = [];

    /// <summary>
    /// Gets expected destination paths in inventory order, with canonical casing on Windows,
    /// for a write-if-different output inventory.
    /// </summary>
    [Output]
    public ITaskItem[] OutputInventoryLines { get; private set; } = [];

    /// <summary>
    /// Validates mapping boundaries and rejects linked layout paths before reporting owned state.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when discovery succeeds; otherwise logs errors and returns
    /// <see langword="false"/>. Outputs must only be consumed after success.
    /// </returns>
    public override bool Execute()
    {
        try
        {
            if (!IncrementalLayoutState.TryGetFullPath(Log, LayoutRoot, nameof(LayoutRoot), out string root))
            {
                return false;
            }

            // Discovery is read-only. Check shared ancestors once per execution, never across builds.
            var validatedPaths = new HashSet<string>(IncrementalLayoutState.PathComparer);
            WorkloadManifestLayoutPaths.ValidateNoLinks(root, root, validatedPaths);
            var sources = new List<ITaskItem>();
            foreach (ITaskItem source in SourceFiles)
            {
                if (!IncrementalLayoutState.TryGetFullPath(Log, source.ItemSpec, nameof(SourceFiles), out string sourcePath)
                    || !IncrementalLayoutState.TryGetFullPath(Log, source.GetMetadata("DestinationPath"), "DestinationPath", out string destination))
                {
                    continue;
                }

                if (!IsOwnedPath(root, destination) || WorkloadManifestLayoutPaths.IsWithin(root, sourcePath))
                {
                    Log.LogError($"Invalid workload manifest layout mapping '{sourcePath}' to '{destination}' under '{root}'.");
                    continue;
                }

                WorkloadManifestLayoutPaths.ValidateNoLinks(root, destination, validatedPaths);
                var input = new TaskItem(source) { ItemSpec = sourcePath };
                input.SetMetadata("DestinationPath", destination);
                sources.Add(input);
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            SourceFilesWithDestinations = sources
                .OrderBy(item => item.GetMetadata("DestinationPath"), IncrementalLayoutState.PathComparer)
                .ToArray();
            InputManifestLines = new[] { $"Root={CanonicalPath(root)}" }
                .Concat(SourceFilesWithDestinations.Select(item =>
                    $"{CanonicalPath(item.ItemSpec)}|{CanonicalPath(item.GetMetadata("DestinationPath"))}"))
                .Select(line => (ITaskItem)new TaskItem(line)).ToArray();
            OutputInventoryLines = SourceFilesWithDestinations
                .Select(item => (ITaskItem)new TaskItem(CanonicalPath(item.GetMetadata("DestinationPath")))).ToArray();

            var files = new List<string>();
            var directories = new HashSet<string>(IncrementalLayoutState.PathComparer);
            if (Directory.Exists(root))
            {
                foreach (string band in Directory.EnumerateDirectories(root).Where(path => IsFeatureBand(Path.GetFileName(path))))
                {
                    WorkloadManifestLayoutPaths.ValidateNoLinks(root, band, validatedPaths);
                    directories.Add(band);
                    foreach (string manifest in Directory.EnumerateDirectories(band).Where(path => IsManifestId(Path.GetFileName(path))))
                    {
                        WorkloadManifestLayoutPaths.ValidateNoLinks(root, manifest, validatedPaths);
                        directories.Add(manifest);
                        foreach (string version in Directory.EnumerateDirectories(manifest).Where(path => IsVersion(Path.GetFileName(path))))
                        {
                            WorkloadManifestLayoutPaths.ValidateNoLinks(root, version, validatedPaths);
                            directories.Add(version);
                            if (IsWorkloadSets(Path.GetFileName(manifest)))
                            {
                                string baseline = Path.Combine(version, "baseline.workloadset.json");
                                WorkloadManifestLayoutPaths.ValidateNoLinks(root, baseline, validatedPaths);
                                if (File.Exists(baseline))
                                {
                                    files.Add(baseline);
                                }
                            }
                            else
                            {
                                EnumeratePayload(root, version, files, directories, validatedPaths);
                            }
                        }
                    }
                }
            }

            ExistingOutputs = files.Order(IncrementalLayoutState.PathComparer).Select(path => (ITaskItem)new TaskItem(path)).ToArray();
            StaleOutputs = ExistingOutputs.ExceptBy(
                SourceFilesWithDestinations.Select(item => item.GetMetadata("DestinationPath")),
                item => item.ItemSpec,
                IncrementalLayoutState.PathComparer).ToArray();
            OwnedDirectories = directories.Order(IncrementalLayoutState.PathComparer).Select(path => (ITaskItem)new TaskItem(path)).ToArray();
            EmptyDirectories = OwnedDirectories.Where(item => !Directory.EnumerateFileSystemEntries(item.ItemSpec).Any()).ToArray();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }

    private static void EnumeratePayload(string root, string directory, List<string> files, HashSet<string> directories, HashSet<string> validatedPaths)
    {
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            WorkloadManifestLayoutPaths.ValidateNoLinks(root, entry, validatedPaths);
            if (Directory.Exists(entry))
            {
                directories.Add(entry);
                EnumeratePayload(root, entry, files, directories, validatedPaths);
            }
            else
            {
                files.Add(entry);
            }
        }
    }

    private static bool IsOwnedPath(string root, string path)
    {
        if (!WorkloadManifestLayoutPaths.IsWithin(root, path))
        {
            return false;
        }

        string[] parts = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 4
            && IsFeatureBand(parts[0])
            && IsManifestId(parts[1])
            && IsVersion(parts[2])
            && (!IsWorkloadSets(parts[1])
                || (parts.Length == 4 && IncrementalLayoutState.PathComparer.Equals(parts[3], "baseline.workloadset.json")));
    }

    private static bool IsVersion(string value) =>
        Regex.IsMatch(value, @"\A[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:[-+].+)?\z", RegexOptions.CultureInvariant)
        && NuGetVersion.TryParse(value, out _);

    private static bool IsFeatureBand(string value) =>
        Regex.IsMatch(value, @"\A[0-9]+\.[0-9]+\.[0-9]+(?:-.+)?\z", RegexOptions.CultureInvariant)
        && NuGetVersion.TryParse(value, out NuGetVersion? version)
        && version.Patch % 100 == 0;

    private static bool IsManifestId(string value) =>
        Regex.IsMatch(value, @"\A[a-zA-Z0-9][a-zA-Z0-9._-]*\z", RegexOptions.CultureInvariant);

    private static bool IsWorkloadSets(string value) =>
        string.Equals(value, "workloadsets", StringComparison.OrdinalIgnoreCase);

    private static string CanonicalPath(string value) =>
        OperatingSystem.IsWindows() ? value.ToUpperInvariant() : value;
}

/// <summary>
/// Removes empty descendant directories deepest-first without recursively deleting their contents.
/// </summary>
/// <remarks>
/// Callers determine which directories they own. This task enforces containment, rejects linked
/// paths, and rechecks each directory before deletion; it never removes the layout root.
/// </remarks>
public sealed class PruneEmptyLayoutDirectories : Task
{
    /// <summary>
    /// Gets or sets the fully qualified staging root that bounds directory pruning.
    /// </summary>
    [Required]
    public string LayoutRoot { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets fully qualified owned descendant directories to consider for removal.
    /// Missing and nonempty directories are left alone.
    /// </summary>
    public ITaskItem[] Directories { get; set; } = [];

    /// <summary>
    /// Validates all candidate paths, then deletes only directories that are still empty.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> on success; otherwise logs errors and returns
    /// <see langword="false"/>. An I/O failure during pruning can leave some candidates unprocessed.
    /// </returns>
    public override bool Execute()
    {
        try
        {
            if (!IncrementalLayoutState.TryGetFullPath(Log, LayoutRoot, nameof(LayoutRoot), out string root))
            {
                return false;
            }

            var paths = new HashSet<string>(IncrementalLayoutState.PathComparer);
            foreach (ITaskItem directory in Directories)
            {
                if (!IncrementalLayoutState.TryGetFullPath(Log, directory.ItemSpec, nameof(Directories), out string path))
                {
                    continue;
                }

                if (!WorkloadManifestLayoutPaths.IsWithin(root, path))
                {
                    Log.LogError($"Cannot prune directory '{path}' outside layout root '{root}'.");
                    continue;
                }

                WorkloadManifestLayoutPaths.ValidateNoLinks(root, path);
                paths.Add(path);
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            foreach (string path in paths.OrderByDescending(path => path.Length))
            {
                WorkloadManifestLayoutPaths.ValidateNoLinks(root, path);
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                {
                    // Never recursively remove a directory: it may contain a sibling's output.
                    Directory.Delete(path, recursive: false);
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }
}

/// <summary>
/// Provides containment and link checks for normalized workload manifest layout paths.
/// </summary>
internal static class WorkloadManifestLayoutPaths
{
    /// <summary>
    /// Tests lexical containment of a descendant path, excluding the root itself.
    /// This check does not resolve or validate symbolic links.
    /// </summary>
    /// <param name="root">The normalized, fully qualified layout root.</param>
    /// <param name="path">The normalized, fully qualified candidate path.</param>
    /// <returns>
    /// Whether the candidate is below the root, using case-insensitive comparison on Windows
    /// and ordinal comparison elsewhere.
    /// </returns>
    public static bool IsWithin(string root, string path) =>
        path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Rejects existing reparse points from the candidate path through the layout root, inclusive.
    /// Ancestors above the root are not checked.
    /// </summary>
    /// <param name="root">The normalized, fully qualified layout root.</param>
    /// <param name="path">A normalized path already known to be the root or one of its descendants.</param>
    /// <param name="validatedPaths">
    /// Optional cache shared only within one read-only discovery execution for the same root.
    /// Omit it when rechecking paths before mutation.
    /// </param>
    /// <exception cref="IOException">An existing path component is a reparse point.</exception>
    public static void ValidateNoLinks(string root, string path, HashSet<string>? validatedPaths = null)
    {
        root = Path.TrimEndingDirectorySeparator(root);
        for (string? current = Path.TrimEndingDirectorySeparator(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if (validatedPaths?.Contains(current) == true)
            {
                break;
            }

            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Workload manifest layout cannot traverse linked path '{current}'.");
            }

            validatedPaths?.Add(current);
            if (IncrementalLayoutState.PathComparer.Equals(current, root))
            {
                break;
            }
        }
    }
}

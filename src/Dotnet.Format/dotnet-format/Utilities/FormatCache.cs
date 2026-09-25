// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    /// <summary>
    /// An on-disk cache that remembers the sha256 hash of each formatted file so that files whose
    /// content already matches what the tool produced can be skipped entirely on the next run.
    /// The cache is keyed on the tool version, the fix category and the formatting inputs
    /// (include/exclude globs plus the applicable .editorconfig files), so any change to those
    /// invalidates the cache.
    /// </summary>
    internal sealed class FormatCache : IDisposable
    {
        private const int FormatCacheVersion = 1;

        private readonly string _cacheFilePath;
        private readonly string _key;
        private readonly bool _writable;
        private readonly ILogger _logger;
        private Dictionary<string, string> _files;
        private bool _dirty;

        private FormatCache(string cacheFilePath, string key, bool writable, Dictionary<string, string> files, ILogger logger)
        {
            _cacheFilePath = cacheFilePath;
            _key = key;
            _writable = writable;
            _files = files;
            _logger = logger;
        }

        /// <summary>
        /// Creates a cache for the given workspace, or returns null when caching is disabled.
        /// Reading from the cache is allowed even when changes are not saved; writing is only
        /// performed when <see cref="FormatOptions.SaveFormattedFiles"/> is true.
        /// </summary>
        public static FormatCache? Create(FormatOptions formatOptions, ILogger logger)
        {
            if (formatOptions.NoCache || string.IsNullOrWhiteSpace(formatOptions.WorkspaceFilePath))
            {
                return null;
            }

            var cacheKey = ComputeCacheKey(formatOptions);

            var cacheDirectory = Path.Combine(Path.GetTempPath(), "dotnet-format");
            Directory.CreateDirectory(cacheDirectory);

            var workspaceHash = ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(formatOptions.WorkspaceFilePath)));
            var cacheFilePath = Path.Combine(cacheDirectory, $"{workspaceHash}.json");

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    var cacheData = JsonSerializer.Deserialize<FormatCacheData>(File.ReadAllText(cacheFilePath));
                    if (cacheData?.Key == cacheKey && cacheData.Version == FormatCacheVersion)
                    {
                        files = cacheData.Files ?? files;
                    }
                }
                catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    logger.LogDebug(Resources.Format_cache_is_corrupt_and_will_be_recreated, cacheFilePath, exception.Message);
                }
            }

            return new FormatCache(cacheFilePath, cacheKey, writable: formatOptions.SaveFormattedFiles, files, logger);
        }

        /// <summary>
        /// Returns true when the file on disk matches the content recorded after the previous
        /// successful formatting run, meaning it can be skipped without being parsed again.
        /// </summary>
        public bool IsUpToDate(string filePath)
        {
            if (!_files.TryGetValue(filePath, out var expectedHash))
            {
                return false;
            }

            return ComputeFileHash(filePath) == expectedHash;
        }

        /// <summary>
        /// Records the on-disk content of the given <paramref name="filePaths"/> after formatting
        /// has been applied. Entries for files that are no longer part of the solution are pruned.
        /// </summary>
        public void RecordFormattedFiles(IEnumerable<string> filePaths, IEnumerable<string> solutionFilePaths)
        {
            if (!_writable)
            {
                return;
            }

            var solutionFilePathsSet = new HashSet<string>(solutionFilePaths, StringComparer.OrdinalIgnoreCase);

            // Keep the recorded entries for files that are still part of the solution so that
            // files skipped by a previous run remain cached, and prune files that no longer are.
            var updatedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;
            foreach (var entry in _files)
            {
                if (solutionFilePathsSet.Contains(entry.Key))
                {
                    updatedFiles[entry.Key] = entry.Value;
                }
                else
                {
                    changed = true;
                }
            }

            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath) || !solutionFilePathsSet.Contains(filePath))
                {
                    continue;
                }

                var fileHash = ComputeFileHash(filePath);
                if (updatedFiles.TryGetValue(filePath, out var existingHash) && existingHash == fileHash)
                {
                    continue;
                }

                updatedFiles[filePath] = fileHash;
                changed = true;
            }

            if (changed)
            {
                _dirty = true;
                _files = updatedFiles;
            }
        }

        public void Dispose()
        {
            if (!_writable || !_dirty)
            {
                return;
            }

            try
            {
                var tempFilePath = $"{_cacheFilePath}.tmp";
                var json = JsonSerializer.Serialize(new FormatCacheData(FormatCacheVersion, _key, _files));
                File.WriteAllText(tempFilePath, json);
                File.Move(tempFilePath, _cacheFilePath, overwrite: true);
                _logger.LogDebug(Resources.Formatting_cache_saved_to_0, _cacheFilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogDebug(Resources.Failed_to_save_the_formatting_cache_to_0_with_1, _cacheFilePath, exception.Message);
            }
        }

        private static string ComputeCacheKey(FormatOptions formatOptions)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(Encoding.UTF8.GetBytes(FormatCommandCommon.GetVersion() ?? string.Empty));
            hash.AppendData(BitConverter.GetBytes((int)formatOptions.FixCategory));
            hash.AppendData(BitConverter.GetBytes(formatOptions.IncludeGeneratedFiles));
            AppendSorted(hash, formatOptions.FileMatcher.Include);
            AppendSorted(hash, formatOptions.FileMatcher.Exclude);
            AppendEditorConfigs(hash, formatOptions.WorkspaceFilePath);
            return Convert.ToHexString(hash.GetHashAndReset());
        }

        private static void AppendEditorConfigs(IncrementalHash hash, string workspaceFilePath)
        {
            var workspaceDirectory = Directory.Exists(workspaceFilePath)
                ? Path.GetFullPath(workspaceFilePath)
                : Path.GetDirectoryName(Path.GetFullPath(workspaceFilePath));

            if (string.IsNullOrEmpty(workspaceDirectory))
            {
                return;
            }

            var editorConfigPaths = new List<string>();

            // Editor configs that apply from the workspace directory up to the drive root.
            var directory = new DirectoryInfo(workspaceDirectory);
            while (directory is not null)
            {
                var editorConfigPath = Path.Combine(directory.FullName, ".editorconfig");
                if (File.Exists(editorConfigPath))
                {
                    editorConfigPaths.Add(editorConfigPath);
                }

                directory = directory.Parent;
            }

            // Editor configs beneath the workspace directory, sorted for a stable key.
            if (Directory.Exists(workspaceDirectory))
            {
                foreach (var editorConfigPath in Directory.EnumerateFiles(workspaceDirectory, ".editorconfig", SearchOption.AllDirectories))
                {
                    var parentDirectory = Path.GetDirectoryName(editorConfigPath);
                    if (parentDirectory is not null && IsIgnoredDirectory(parentDirectory))
                    {
                        continue;
                    }

                    editorConfigPaths.Add(editorConfigPath);
                }

                editorConfigPaths.Sort(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var editorConfigPath in editorConfigPaths)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(editorConfigPath));

                try
                {
                    hash.AppendData(File.ReadAllBytes(editorConfigPath));
                }
                catch (IOException)
                {
                    // A config may disappear between enumeration and hashing; ignore it.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore configs that cannot be read.
                }
            }
        }

        private static void AppendSorted(IncrementalHash hash, ImmutableArray<string> values)
        {
            foreach (var value in values.OrderBy(value => value, StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(value));
            }
        }

        private static bool IsIgnoredDirectory(string directoryPath)
        {
            var name = Path.GetFileName(directoryPath);
            return name.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || name.Equals(".vs", StringComparison.OrdinalIgnoreCase)
                || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeFileHash(string filePath)
        {
            try
            {
                return ComputeHash(File.ReadAllBytes(filePath));
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static string ComputeHash(byte[] data)
            => Convert.ToHexString(SHA256.HashData(data));

        private sealed record FormatCacheData(int Version, string Key, Dictionary<string, string> Files);
    }
}
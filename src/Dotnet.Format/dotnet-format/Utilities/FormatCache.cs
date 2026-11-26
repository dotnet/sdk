// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    /// <summary>
    /// Remembers the exact bytes produced by whitespace formatting in a folder workspace.
    /// Semantic formatting and MSBuild workspaces have dependencies not represented by this cache.
    /// </summary>
    internal sealed class FormatCache : IDisposable
    {
        private const int FormatCacheVersion = 2;
        private static readonly StringComparer s_pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        private readonly string _cacheFilePath;
        private readonly string _key;
        private readonly FormatOptions _formatOptions;
        private readonly ILogger _logger;
        private Dictionary<string, string> _files;
        private bool _dirty;

        private FormatCache(string cacheFilePath, string key, FormatOptions formatOptions, Dictionary<string, string> files, ILogger logger)
        {
            _cacheFilePath = cacheFilePath;
            _key = key;
            _formatOptions = formatOptions;
            _files = files;
            _logger = logger;
        }

        /// <summary>
        /// Creates a cache only for whitespace-only folder formatting. Explicit paths outside
        /// the folder are excluded because their configuration is not covered by the key.
        /// Verification runs may read the cache, but never write it.
        /// </summary>
        public static FormatCache? Create(FormatOptions formatOptions, ILogger logger)
        {
            if (formatOptions.NoCache || string.IsNullOrWhiteSpace(formatOptions.WorkspaceFilePath) ||
                formatOptions.WorkspaceType != WorkspaceType.Folder || formatOptions.FixCategory != FixCategory.Whitespace ||
                formatOptions.FileMatcher.Include.Any(path => Path.IsPathRooted(path) || path.Split('/', '\\').Contains("..")))
            {
                return null;
            }

            try
            {
                var cacheKey = ComputeCacheKey(formatOptions);
                var cacheDirectory = Path.Combine(Path.GetTempPath(), "dotnet-format");
                Directory.CreateDirectory(cacheDirectory);
                var workspaceHash = ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(formatOptions.WorkspaceFilePath)));
                var cacheFilePath = Path.Combine(cacheDirectory, $"{workspaceHash}.json");
                var files = new Dictionary<string, string>(s_pathComparer);
                if (File.Exists(cacheFilePath))
                {
                    try
                    {
                        var cacheData = JsonSerializer.Deserialize<FormatCacheData>(File.ReadAllText(cacheFilePath));
                        if (cacheData?.Key == cacheKey && cacheData.Version == FormatCacheVersion && cacheData.Files is not null)
                        {
                            files = new Dictionary<string, string>(cacheData.Files, s_pathComparer);
                        }
                    }
                    catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
                    {
                        logger.LogDebug(Resources.Format_cache_is_corrupt_and_will_be_recreated, cacheFilePath, exception.Message);
                    }
                }

                return new FormatCache(cacheFilePath, cacheKey, formatOptions, files, logger);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // An unavailable cache or unreadable configuration must not prevent formatting.
                logger.LogDebug("Formatting cache unavailable: {Message}", exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Validates configuration once for a document-selection batch. Recording and publishing
        /// independently revalidate it so concurrent configuration edits cannot poison the cache.
        /// </summary>
        public HashSet<string> GetUpToDateFiles(IEnumerable<string> filePaths)
        {
            var result = new HashSet<string>(s_pathComparer);
            if (!ConfigurationIsUnchanged())
            {
                return result;
            }

            foreach (var filePath in filePaths)
            {
                if (_files.TryGetValue(filePath, out var expectedHash) &&
                    !string.IsNullOrEmpty(expectedHash) && ComputeFileHash(filePath) == expectedHash)
                {
                    result.Add(filePath);
                }
            }

            return result;
        }

        /// <summary>
        /// Records only files whose disk bytes match the formatter's expected output, never an
        /// arbitrary post-save disk snapshot. The caller must capture these texts from the
        /// formatted solution before applying changes, including unchanged formattable documents.
        /// </summary>
        public void RecordFormattedFiles(IEnumerable<KeyValuePair<string, SourceText>> formattedFiles, IEnumerable<string> solutionFilePaths)
        {
            if (!_formatOptions.SaveFormattedFiles || !ConfigurationIsUnchanged())
            {
                return;
            }

            var solutionFilePathsSet = new HashSet<string>(solutionFilePaths, s_pathComparer);
            var updatedFiles = new Dictionary<string, string>(s_pathComparer);
            foreach (var entry in _files)
            {
                if (solutionFilePathsSet.Contains(entry.Key))
                {
                    updatedFiles[entry.Key] = entry.Value;
                }
            }

            foreach (var (filePath, text) in formattedFiles)
            {
                // Remove any stale entry even when the save failed or the file was edited.
                updatedFiles.Remove(filePath);
                if (!solutionFilePathsSet.Contains(filePath) || text.Encoding is null)
                {
                    continue;
                }

                using var stream = new MemoryStream();
                using (var writer = new StreamWriter(stream, text.Encoding, leaveOpen: true))
                {
                    text.Write(writer);
                }

                var expectedHash = ComputeHash(stream.ToArray());
                if (ComputeFileHash(filePath) == expectedHash)
                {
                    updatedFiles[filePath] = expectedHash;
                }
            }

            _dirty |= _files.Count != updatedFiles.Count ||
                _files.Any(entry => !updatedFiles.TryGetValue(entry.Key, out var value) || value != entry.Value);
            _files = updatedFiles;
        }

        public void Dispose()
        {
            if (!_formatOptions.SaveFormattedFiles || !_dirty || !ConfigurationIsUnchanged())
            {
                return;
            }

            var tempFilePath = $"{_cacheFilePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                var json = JsonSerializer.Serialize(new FormatCacheData(FormatCacheVersion, _key, _files));
                using (var stream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                }

                File.Move(tempFilePath, _cacheFilePath, overwrite: true);
                _logger.LogDebug(Resources.Formatting_cache_saved_to_0, _cacheFilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogDebug(Resources.Failed_to_save_the_formatting_cache_to_0_with_1, _cacheFilePath, exception.Message);
            }
            finally
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    _logger.LogDebug(Resources.Failed_to_save_the_formatting_cache_to_0_with_1, tempFilePath, exception.Message);
                }
            }
        }

        private bool ConfigurationIsUnchanged()
        {
            try
            {
                return ComputeCacheKey(_formatOptions) == _key;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return false;
            }
        }

        private static string ComputeCacheKey(FormatOptions formatOptions)
        {
            // JSON provides field and element boundaries (concatenating globs is ambiguous).
            var options = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Version = FormatCommandCommon.GetVersion(),
                formatOptions.WorkspaceType,
                formatOptions.FixCategory,
                formatOptions.IncludeGeneratedFiles,
                formatOptions.CodeStyleSeverity,
                formatOptions.AnalyzerSeverity,
                Diagnostics = formatOptions.Diagnostics.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ExcludeDiagnostics = formatOptions.ExcludeDiagnostics.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Include = formatOptions.FileMatcher.Include.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Exclude = formatOptions.FileMatcher.Exclude.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                formatOptions.TargetFramework,
            });
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(options);
            AppendEditorConfigs(hash, formatOptions.WorkspaceFilePath);
            return Convert.ToHexString(hash.GetHashAndReset());
        }

        private static void AppendEditorConfigs(IncrementalHash hash, string workspaceDirectory)
        {
            var editorConfigPaths = new HashSet<string>(s_pathComparer);
            var directory = new DirectoryInfo(Path.GetFullPath(workspaceDirectory));
            while (directory is not null)
            {
                var editorConfigPath = Path.Combine(directory.FullName, ".editorconfig");
                if (File.Exists(editorConfigPath))
                {
                    editorConfigPaths.Add(editorConfigPath);
                }

                directory = directory.Parent;
            }

            // Folder mode can explicitly include files in normally ignored directories too.
            foreach (var editorConfigPath in Directory.EnumerateFiles(workspaceDirectory, ".editorconfig", SearchOption.AllDirectories))
            {
                editorConfigPaths.Add(Path.GetFullPath(editorConfigPath));
            }

            foreach (var editorConfigPath in editorConfigPaths.OrderBy(path => path, StringComparer.Ordinal))
            {
                var pathBytes = Encoding.UTF8.GetBytes(editorConfigPath);
                var content = File.ReadAllBytes(editorConfigPath);
                hash.AppendData(BitConverter.GetBytes(pathBytes.Length));
                hash.AppendData(pathBytes);
                hash.AppendData(BitConverter.GetBytes(content.Length));
                hash.AppendData(content);
            }
        }

        private static string? ComputeFileHash(string filePath)
        {
            try
            {
                return ComputeHash(File.ReadAllBytes(filePath));
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string ComputeHash(byte[] data)
            => Convert.ToHexString(SHA256.HashData(data));

        private sealed record FormatCacheData(int Version, string Key, Dictionary<string, string> Files);
    }
}

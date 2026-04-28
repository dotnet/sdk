// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// JSON serialization context for the download cache index.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class DownloadCacheJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Manages a cache of downloaded .NET archives to avoid re-downloading files.
/// </summary>
internal class DownloadCache
{
    private readonly string _cacheDirectory;
    private readonly string _cacheIndexPath;
    private readonly Dictionary<string, string> _cacheIndex;

    public DownloadCache(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "dotnetup",
            "downloadcache");
        _cacheIndexPath = Path.Combine(_cacheDirectory, "cache-index.json");
        _cacheIndex = LoadCacheIndex();
    }

    /// <summary>
    /// Gets the path to a cached file for the given URL, if it exists.
    /// </summary>
    /// <param name="downloadUrl">The URL that was used to download the file</param>
    /// <returns>The path to the cached file, or null if not found</returns>
    public string? GetCachedFilePath(string downloadUrl)
    {
        if (_cacheIndex.TryGetValue(downloadUrl, out string? fileName))
        {
            string filePath = Path.Combine(_cacheDirectory, fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }
            // File was deleted, remove from index
            _cacheIndex.Remove(downloadUrl);
            SaveCacheIndex();
        }
        return null;
    }

    /// <summary>
    /// Adds a file to the cache.
    /// </summary>
    /// <param name="downloadUrl">The URL the file was downloaded from</param>
    /// <param name="sourceFilePath">The path to the file to cache</param>
    public void AddToCache(string downloadUrl, string sourceFilePath)
    {
        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);

        // Use the filename from the download URL
        string fileName = GetFileNameFromUrl(downloadUrl);
        string cachedFilePath = Path.Combine(_cacheDirectory, fileName);

        // Skip if this filename is already cached for a different URL
        // (collision case - we'll download the right file when needed and hash check will catch it)
        if (_cacheIndex.ContainsValue(fileName) && !_cacheIndex.ContainsKey(downloadUrl))
        {
            return;
        }

        // Copy the file to the cache
        File.Copy(sourceFilePath, cachedFilePath, overwrite: true);

        // Update the index
        _cacheIndex[downloadUrl] = fileName;
        SaveCacheIndex();
    }

    /// <summary>
    /// Extracts the filename from a download URL.
    /// </summary>
    private static string GetFileNameFromUrl(string downloadUrl)
    {
        Uri uri = new Uri(downloadUrl);
        string fileName = Path.GetFileName(uri.LocalPath);

        // Fallback to a default name if we can't extract a filename
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "download.dat";
        }

        return fileName;
    }

    /// <summary>
    /// Loads the cache index from disk.
    /// </summary>
    private Dictionary<string, string> LoadCacheIndex()
    {
        if (!File.Exists(_cacheIndexPath))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(_cacheIndexPath);
            var index = JsonSerializer.Deserialize(json, DownloadCacheJsonContext.Default.DictionaryStringString);
            return index ?? [];
        }
        catch
        {
            // If the index is corrupted, start fresh
            return [];
        }
    }

    /// <summary>
    /// Saves the cache index to disk.
    /// </summary>
    private void SaveCacheIndex()
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            string json = JsonSerializer.Serialize(_cacheIndex, DownloadCacheJsonContext.Default.DictionaryStringString);
            File.WriteAllText(_cacheIndexPath, json);
        }
        catch
        {
            // Ignore errors saving the cache index - it's not critical
        }
    }
}

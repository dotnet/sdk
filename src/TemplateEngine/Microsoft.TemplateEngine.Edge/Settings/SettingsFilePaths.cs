// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    internal class SettingsFilePaths
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private string? _aliasesFile;
        private string? _firstRunCookie;
        private string? _scratchDir;
        private string? _settingsFile;
        private string? _contentDir;
        private string? _templatesCacheFile;

        internal SettingsFilePaths(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
        }

        internal string AliasesFile => GetOrComputePath(ref _aliasesFile, BaseDir, "aliases.json");

        internal string BaseDir => _environmentSettings.Paths.HostVersionSettingsDir;

        internal string Content => GetOrComputePath(ref _contentDir, BaseDir, "content");

        internal string FirstRunCookie => GetOrComputePath(ref _firstRunCookie, BaseDir, ".firstrun");

        internal string ScratchDir => GetOrComputePath(ref _scratchDir, BaseDir, "scratch");

        internal string SettingsFile => GetOrComputePath(ref _settingsFile, BaseDir, "settings.json");

        internal string TemplateCacheFile => GetOrComputePath(ref _templatesCacheFile, BaseDir, "templatecache.json");

        internal string ProcessPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path[0] != '~')
            {
                return path;
            }

            return Path.Combine(_environmentSettings.Paths.UserProfileDir, path.Substring(1));
        }

        internal void Copy(string path, string targetPath)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(path))
            {
                _environmentSettings.Host.FileSystem.FileCopy(path, targetPath, true);
                return;
            }

            foreach (string p in EnumerateFiles(path, "*", SearchOption.AllDirectories).OrderBy(x => x.Length))
            {
                string localPath = p.Substring(path.Length).TrimStart('\\', '/');

                if (_environmentSettings.Host.FileSystem.DirectoryExists(p))
                {
                    CreateDirectory(localPath, targetPath);
                }
                else
                {
                    int parentDirEndIndex = localPath.LastIndexOfAny(new[] { '/', '\\' });
                    string wholeTargetPath = Path.Combine(targetPath, localPath);

                    if (parentDirEndIndex > -1)
                    {
                        string parentDir = localPath.Substring(0, parentDirEndIndex);
                        CreateDirectory(parentDir, targetPath);
                    }

                    _environmentSettings.Host.FileSystem.FileCopy(p, wholeTargetPath, true);
                }
            }
        }

        internal Stream OpenRead(string path)
        {
            return _environmentSettings.Host.FileSystem.OpenRead(path);
        }

#pragma warning disable SA1011 // Closing square brackets should be spaced correctly - conflicting analyzer rules
        internal byte[]? ReadAllBytes(string path)
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
        {
            if (Exists(path))
            {
                using (Stream s = _environmentSettings.Host.FileSystem.OpenRead(path))
                {
                    byte[] buffer = new byte[s.Length];
                    s.Read(buffer, 0, buffer.Length);
                    return buffer;
                }
            }
            return null;
        }

        internal void CreateDirectory(string path, string parent)
        {
            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string current = parent;

            for (int i = 0; i < parts.Length; ++i)
            {
                current = Path.Combine(current, parts[i]);
                _environmentSettings.Host.FileSystem.CreateDirectory(current);
            }
        }

        internal void CreateDirectory(string path)
        {
            _environmentSettings.Host.FileSystem.CreateDirectory(path);
        }

        internal void Delete(string path)
        {
            DeleteDirectory(path);
            DeleteFile(path);
        }

        internal void DeleteDirectory(string path)
        {
            if (_environmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                _environmentSettings.Host.FileSystem.DirectoryDelete(path, true);
            }
        }

        internal void DeleteFile(string path)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(path))
            {
                _environmentSettings.Host.FileSystem.FileDelete(path);
            }
        }

        internal bool DirectoryExists(string path)
        {
            return _environmentSettings.Host.FileSystem.DirectoryExists(path);
        }

        internal IEnumerable<string> EnumerateDirectories(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_environmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return _environmentSettings.Host.FileSystem.EnumerateDirectories(path, pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        internal IEnumerable<string> EnumerateFiles(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(path))
            {
                return new[] { path };
            }

            if (_environmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return _environmentSettings.Host.FileSystem.EnumerateFiles(ProcessPath(path), pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        internal IEnumerable<string> EnumerateFileSystemEntries(string path, string pattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (_environmentSettings.Host.FileSystem.FileExists(path))
            {
                return new[] { path };
            }

            if (_environmentSettings.Host.FileSystem.DirectoryExists(path))
            {
                return _environmentSettings.Host.FileSystem.EnumerateFileSystemEntries(ProcessPath(path), pattern, searchOption);
            }

            return Enumerable.Empty<string>();
        }

        internal bool Exists(string path)
        {
            return FileExists(path) || DirectoryExists(path);
        }

        internal bool FileExists(string path)
        {
            return _environmentSettings.Host.FileSystem.FileExists(path);
        }

        internal string Name(string path)
        {
            path = path.TrimEnd('/', '\\');
            return Path.GetFileName(path);
        }

        internal string ReadAllText(string path, string defaultValue = "")
        {
            return Exists(path) ? _environmentSettings.Host.FileSystem.ReadAllText(path) : defaultValue;
        }

        internal void WriteAllText(string path, string value)
        {
            string parentDir = Path.GetDirectoryName(path);

            if (!Exists(parentDir))
            {
                _environmentSettings.Host.FileSystem.CreateDirectory(parentDir);
            }

            _environmentSettings.Host.FileSystem.WriteAllText(path, value);
        }

        private string GetOrComputePath(ref string? cache, params string[] paths)
        {
            return cache ?? (cache = Path.Combine(paths));
        }
    }
}

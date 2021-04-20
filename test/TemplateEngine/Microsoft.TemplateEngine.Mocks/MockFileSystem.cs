// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockFileSystem : IPhysicalFileSystem, IFileLastWriteTimeSource
    {
        private class FileSystemFile
        {
            public FileSystemFile()
            {
            }

            public FileSystemFile(FileSystemFile file)
            {
                Data = file.Data;
                Attributes = file.Attributes;
                LastWriteTimeUtc = file.LastWriteTimeUtc;
            }

            public byte[] Data;
            public FileAttributes Attributes;

            public DateTime LastWriteTimeUtc;
        }

        public class DirectoryScanParameters
        {
            public string DirectoryName { get; set; }
            public string Pattern { get; set; }
            public SearchOption SearchOption { get; set; }
        }

        private HashSet<string> _directories = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, FileSystemFile> _files = new Dictionary<string, FileSystemFile>(StringComparer.Ordinal);
        private List<DirectoryScanParameters> _directoriesScanned = new List<DirectoryScanParameters>();

        public MockFileSystem Add(string filePath, string contents, Encoding encoding = null, DateTime? lastWriteTime = null)
        {
            _files[filePath] = new FileSystemFile()
            {
                Data = (encoding ?? Encoding.UTF8).GetBytes(contents),
                LastWriteTimeUtc = lastWriteTime ?? DateTime.UtcNow
            };

            string currentParent = Path.GetDirectoryName(filePath);

            while (currentParent.IndexOfAny(new[] { '\\', '/' }) != currentParent.Length - 1)
            {
                _directories.Add(currentParent);
                currentParent = Path.GetDirectoryName(currentParent);
            }

            return this;
        }

        public bool DirectoryExists(string directory)
        {
            return _directories.Contains(directory);
        }

        public bool FileExists(string file)
        {
            return _files.ContainsKey(file);
        }

        public void FileDelete(string file)
        {
            if (!_files.ContainsKey(file))
            {
                throw new Exception($"File {file} does not exist");
            }

            _files.Remove(file);
        }

        public Stream CreateFile(string path)
        {
            if (!FileExists(path))
            {
                _files[path] = new FileSystemFile();
            }
            _files[path].Data = new byte[0];
            return new MockFileStream(d => _files[path].Data = d);
        }

        public Stream OpenRead(string path)
        {
            if (!_files.TryGetValue(path, out FileSystemFile file))
            {
                throw new Exception($"File {path} does no texist");
            }

            MemoryStream s = new MemoryStream(file.Data);
            return s;
        }

        public void CreateDirectory(string path)
        {
            _directories.Add(path);
        }

        public string CurrentDirectory { get; set; }

        public string GetCurrentDirectory()
        {
            return CurrentDirectory;
        }

        public IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption searchOption)
        {
            return _files.Keys.Where(x => x.StartsWith(path, StringComparison.Ordinal) && (x[path.Length] == Path.DirectorySeparatorChar || x[path.Length] == Path.AltDirectorySeparatorChar));
        }

        public IEnumerable<string> EnumerateDirectories(string path, string pattern, SearchOption searchOption)
        {
            return _directories.Where(x => x.StartsWith(path, StringComparison.Ordinal) && (x[path.Length] == Path.DirectorySeparatorChar || x[path.Length] == Path.AltDirectorySeparatorChar));
        }

        public void WriteAllText(string path, string value)
        {
            if (!FileExists(path))
            {
                _files[path] = new FileSystemFile();
            }

            _files[path].Data = Encoding.UTF8.GetBytes(value);
        }

        public string ReadAllText(string path)
        {
            return Encoding.UTF8.GetString(_files[path].Data);
        }

        public void DirectoryDelete(string path, bool recursive)
        {
            if (!recursive
                && _files.Any(x => x.Key.StartsWith(path, StringComparison.Ordinal) && (x.Key[path.Length] == Path.DirectorySeparatorChar || x.Key[path.Length] == Path.AltDirectorySeparatorChar))
                && _directories.Any(x => x.StartsWith(path, StringComparison.Ordinal) && (x[path.Length] == Path.DirectorySeparatorChar || x[path.Length] == Path.AltDirectorySeparatorChar))
                )
            {
                throw new Exception("Directory is not empty");
            }

            _directories.RemoveWhere(x => x.Equals(path, StringComparison.Ordinal) || x.StartsWith(path, StringComparison.Ordinal) && (x[path.Length] == Path.DirectorySeparatorChar || x[path.Length] == Path.AltDirectorySeparatorChar));
            List<string> toRemove = new List<string>();

            foreach (string key in _files.Keys)
            {
                if (key.StartsWith(path, StringComparison.Ordinal) && (key[path.Length] == Path.DirectorySeparatorChar || key[path.Length] == Path.AltDirectorySeparatorChar))
                {
                    toRemove.Add(key);
                }
            }

            foreach (string key in toRemove)
            {
                _files.Remove(key);
            }
        }

        public void FileCopy(string sourcePath, string targetPath, bool overwrite)
        {
            if (!overwrite && FileExists(targetPath))
            {
                throw new Exception($"File {targetPath} already exists");
            }

            if (!FileExists(sourcePath))
            {
                throw new Exception($"File {sourcePath} doesn't exist");
            }

            _files[targetPath] = new FileSystemFile(_files[targetPath]);
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string directoryName, string pattern, SearchOption searchOption)
        {
            RecordDirectoryScan(directoryName, pattern, searchOption);
            Glob g = Glob.Parse(searchOption != SearchOption.AllDirectories ? "**/" + pattern : pattern);

            foreach (string entry in _files.Keys.Union(_directories).Where(x => x.StartsWith(directoryName, StringComparison.Ordinal) || x.StartsWith(directoryName, StringComparison.Ordinal) && (x[directoryName.Length] == Path.DirectorySeparatorChar || x[directoryName.Length] == Path.AltDirectorySeparatorChar)))
            {
                string p = entry.Replace('\\', '/').TrimStart('/');
                if (g.IsMatch(p))
                {
                    yield return entry;
                }
            }
        }

        public IReadOnlyList<DirectoryScanParameters> DirectoriesScanned => _directoriesScanned;

        private void RecordDirectoryScan(string directoryName, string pattern, SearchOption searchOption)
        {
            _directoriesScanned.Add(new DirectoryScanParameters
            {
                DirectoryName = directoryName,
                Pattern = pattern,
                SearchOption = searchOption
            });
        }

        public FileAttributes GetFileAttributes(string file)
        {
            if (!FileExists(file))
            {
                throw new Exception($"File {file} doesn't exist");
            }

            return _files[file].Attributes;
        }

        public void SetFileAttributes(string file, FileAttributes attributes)
        {
            if (!FileExists(file))
            {
                throw new Exception($"File {file} doesn't exist");
            }

            _files[file].Attributes = attributes;
        }

        public DateTime GetLastWriteTimeUtc(string file)
        {
            if (!FileExists(file))
            {
                throw new Exception($"File {file} doesn't exist");
            }

            return _files[file].LastWriteTimeUtc;
        }

        public void SetLastWriteTimeUtc(string file, DateTime lastWriteTimeUtc)
        {
            if (!FileExists(file))
            {
                throw new Exception($"File {file} doesn't exist");
            }

            _files[file].LastWriteTimeUtc = lastWriteTimeUtc;
        }

        public IDisposable WatchFileChanges(string filepath, FileSystemEventHandler fileChanged) => throw new NotImplementedException();
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Local file system implementation of <see cref="IPhysicalFileSystem"/>.
    /// </summary>
    /// <seealso cref="Abstractions.ITemplateEngineHost"/>
    public class PhysicalFileSystem : IPhysicalFileSystem
    {
        public bool DirectoryExists(string directory)
        {
            return Directory.Exists(directory);
        }

        public bool FileExists(string file)
        {
            return File.Exists(file);
        }

        public Stream CreateFile(string path)
        {
            return File.Create(path);
        }

        public void CreateDirectory(string path)
        {
            _ = Directory.CreateDirectory(path);
        }

        public string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string directoryName, string pattern, SearchOption searchOption)
        {
            return Directory.EnumerateFileSystemEntries(directoryName, pattern, searchOption);
        }

        public void FileCopy(string source, string target, bool overwrite)
        {
            File.Copy(source, target, overwrite);
        }

        public void DirectoryDelete(string path, bool recursive)
        {
            Directory.Delete(path, recursive);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public void WriteAllText(string path, string value)
        {
            File.WriteAllText(path, value);
        }

        public IEnumerable<string> EnumerateDirectories(string path, string pattern, SearchOption searchOption)
        {
            return Directory.EnumerateDirectories(path, pattern, searchOption);
        }

        public IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, pattern, searchOption);
        }

        public Stream OpenRead(string path)
        {
            return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void FileDelete(string path)
        {
            File.Delete(path);
        }

        public FileAttributes GetFileAttributes(string file)
        {
            return File.GetAttributes(file);
        }

        public void SetFileAttributes(string file, FileAttributes attributes)
        {
            File.SetAttributes(file, attributes);
        }

        public DateTime GetLastWriteTimeUtc(string file)
        {
            return File.GetLastWriteTimeUtc(file);
        }

        public void SetLastWriteTimeUtc(string file, DateTime lastWriteTimeUtc)
        {
            File.SetLastWriteTimeUtc(file, lastWriteTimeUtc);
        }

        public string PathRelativeTo(string target, string relativeTo)
        {
            string resultPath;
            try
            {
                string basePath = Path.GetFullPath(relativeTo);
                string sourceFullPath = Path.GetFullPath(target);
                resultPath = PathRelativeToInternal(sourceFullPath, basePath);
            }
            catch (Exception)
            {
                resultPath = NormalizePath(target);
            }

            return resultPath;
        }

        public IDisposable WatchFileChanges(string filePath, FileSystemEventHandler fileChanged)
        {
            FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath), Path.GetFileName(filePath));
            watcher.Changed += fileChanged;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        internal static string PathRelativeToInternal(string target, string relativeTo)
        {
            string resultPath = target;
            relativeTo = relativeTo.TrimEnd('/', '\\');
            if (target.StartsWith(relativeTo, StringComparison.CurrentCulture))
            {
                resultPath = target.Substring(relativeTo.Length + 1);
            }

            return NormalizePath(resultPath);
        }

        private static string NormalizePath(string path)
        {
            char desiredDirectorySeparator = Path.DirectorySeparatorChar;
            char undesiredSeparatorChar = desiredDirectorySeparator switch
            {
                '/' => '\\',
                '\\' => '/',
                _ => desiredDirectorySeparator,
            };
            return path.Replace(undesiredSeparatorChar, desiredDirectorySeparator);
        }
    }
}

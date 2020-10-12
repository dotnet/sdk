using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Utils
{
    public class InMemoryFileSystem : IPhysicalFileSystem, IFileLastWriteTimeSource
    {
        private class FileSystemDirectory
        {
            public string Name { get; }

            public string FullPath { get; }

            public Dictionary<string, FileSystemDirectory> Directories { get; }

            public Dictionary<string, FileSystemFile> Files { get; }

            public FileSystemDirectory(string name, string fullPath)
            {
                Name = name;
                FullPath = fullPath;
                Directories = new Dictionary<string, FileSystemDirectory>();
                Files = new Dictionary<string, FileSystemFile>();
            }
        }

        private readonly FileSystemDirectory _root;
        private readonly IPhysicalFileSystem _basis;

        private class FileSystemFile
        {
            private byte[] _data;
            private int _currentReaders;
            private int _currentWriters;
            private readonly object _sync = new object();

            public string Name { get; }

            public string FullPath { get; }

            public FileSystemFile(string name, string fullPath)
            {
                Name = name;
                FullPath = fullPath;
                _data = new byte[0];
            }

            public Stream OpenRead()
            {
                if (_currentWriters > 0)
                {
                    throw new IOException("File is currently locked for writing");
                }

                lock (_sync)
                {
                    if (_currentWriters > 0)
                    {
                        throw new IOException("File is currently locked for writing");
                    }

                    ++_currentReaders;
                    return new DisposingStream(new MemoryStream(_data, false), () => { lock (_sync) { --_currentReaders; } });
                }
            }

            public Stream OpenWrite()
            {
                if (_currentReaders > 0)
                {
                    throw new IOException("File is currently locked for reading");
                }

                if (_currentWriters > 0)
                {
                    throw new IOException("File is currently locked for writing");
                }

                lock (_sync)
                {
                    if (_currentReaders > 0)
                    {
                        throw new IOException("File is currently locked for reading");
                    }

                    if (_currentWriters > 0)
                    {
                        throw new IOException("File is currently locked for writing");
                    }

                    ++_currentWriters;
                    MemoryStream target = new MemoryStream();
                    return new DisposingStream(target, () =>
                    {
                        lock (_sync)
                        {
                            --_currentWriters;
                            _data = new byte[target.Length];
                            target.Position = 0;
                            target.Read(_data, 0, _data.Length);
                            LastWriteTimeUtc = DateTime.UtcNow;
                        }
                    });
                }
            }

            public FileAttributes Attributes { get; set; }
            public DateTime LastWriteTimeUtc { get; set; }
        }

        private class DisposingStream : Stream
        {
            private bool _isDisposed;
            private readonly Stream _basis;
            private readonly Action _onDispose;

            public DisposingStream(Stream basis, Action onDispose)
            {
                _onDispose = onDispose;
                _basis = basis;
            }

            public override bool CanRead => _basis.CanRead;

            public override bool CanSeek => _basis.CanSeek;

            public override bool CanWrite => _basis.CanWrite;

            public override long Length => _basis.Length;

            public override long Position { get => _basis.Position; set => _basis.Position = value; }

            public override void Flush() => _basis.Flush();

            public override int Read(byte[] buffer, int offset, int count) => _basis.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => _basis.Seek(offset, origin);

            public override void SetLength(long value) => _basis.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) => _basis.Write(buffer, offset, count);

            protected override void Dispose(bool disposing)
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    _onDispose();
                }

                base.Dispose(disposing);
            }
        }

        public InMemoryFileSystem(string root, IPhysicalFileSystem basis)
        {
            _basis = basis;
            _root = new FileSystemDirectory(Path.GetFileName(root.TrimEnd('/', '\\')), root);
            IsPathInCone(root, out root);
            _root = new FileSystemDirectory(Path.GetFileName(root.TrimEnd('/', '\\')), root);
        }

        public void CreateDirectory(string path)
        {
            if (!IsPathInCone(path, out string processedPath))
            {
                _basis.CreateDirectory(path);
                return;
            }

            path = processedPath;
            string rel = path.Substring(_root.FullPath.Length).Trim('/', '\\');

            if (string.IsNullOrEmpty(rel))
            {
                return;
            }

            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    dir = new FileSystemDirectory(parts[i], Path.Combine(currentDir.FullPath, parts[i]));
                    currentDir.Directories[parts[i]] = dir;
                }

                currentDir = dir;
            }
        }

        public Stream CreateFile(string path)
        {
            if (!IsPathInCone(path, out string processedPath))
            {
                return _basis.CreateFile(path);
            }

            path = processedPath;
            string rel = path.Substring(_root.FullPath.Length).Trim('/', '\\');
            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    dir = new FileSystemDirectory(parts[i], Path.Combine(currentDir.FullPath, parts[i]));
                    currentDir.Directories[parts[i]] = dir;
                }

                currentDir = dir;
            }

            FileSystemFile targetFile;
            if (!currentDir.Files.TryGetValue(parts[parts.Length - 1], out targetFile))
            {
                targetFile = new FileSystemFile(parts[parts.Length - 1], Path.Combine(currentDir.FullPath, parts[parts.Length - 1]));
                currentDir.Files[parts[parts.Length - 1]] = targetFile;
            }

            return targetFile.OpenWrite();
        }

        public void DirectoryDelete(string path, bool recursive)
        {
            if (!IsPathInCone(path, out string processedPath))
            {
                //TODO: handle cases where a portion of what would be deleted is inside our cone
                //  and parts are outside
                _basis.DirectoryDelete(path, recursive);
                return;
            }

            path = processedPath;
            string rel = path.Substring(_root.FullPath.Length).Trim('/', '\\');

            if (string.IsNullOrEmpty(rel))
            {
                return;
            }

            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;
            FileSystemDirectory parent = null;

            for (int i = 0; i < parts.Length; ++i)
            {
                parent = currentDir;
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    return;
                }

                currentDir = dir;
            }

            if (!recursive && (currentDir.Directories.Count > 0 || currentDir.Files.Count > 0))
            {
                throw new IOException("Directory is not empty");
            }

            parent.Directories.Remove(currentDir.Name);
        }

        public bool DirectoryExists(string directory)
        {
            if (!IsPathInCone(directory, out string processedPath))
            {
                return _basis.DirectoryExists(directory);
            }

            directory = processedPath;
            string rel = directory.Substring(_root.FullPath.Length).Trim('/', '\\');

            if (string.IsNullOrEmpty(rel))
            {
                return true;
            }

            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    return false;
                }

                currentDir = dir;
            }

            return true;
        }

        public IEnumerable<string> EnumerateDirectories(string path, string pattern, SearchOption searchOption)
        {
            if (!IsPathInCone(path, out string processedPath))
            {
                //TODO: Handle cases where part of the directory set is inside our cone and part is not
                foreach (string s in _basis.EnumerateDirectories(path, pattern, searchOption))
                {
                    yield return s;
                }

                yield break;
            }

            path = processedPath;
            string rel = path.Substring(_root.FullPath.Length).Trim('/', '\\');
            FileSystemDirectory currentDir = _root;

            if (!string.IsNullOrEmpty(rel))
            {
                string[] parts = rel.Split('/', '\\');
                for (int i = 0; i < parts.Length; ++i)
                {
                    FileSystemDirectory dir;
                    if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                    {
                        yield break;
                    }

                    currentDir = dir;
                }
            }

            Regex rx = new Regex(Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", "."));

            if (searchOption == SearchOption.TopDirectoryOnly)
            {
                foreach (KeyValuePair<string, FileSystemDirectory> entry in currentDir.Directories)
                {
                    if (rx.IsMatch(entry.Key))
                    {
                        yield return entry.Value.FullPath;
                    }
                }

                yield break;
            }

            Stack<IEnumerator<KeyValuePair<string, FileSystemDirectory>>> directories = new Stack<IEnumerator<KeyValuePair<string, FileSystemDirectory>>>();
            IEnumerator<KeyValuePair<string, FileSystemDirectory>> current = currentDir.Directories.GetEnumerator();
            bool moveNext;

            while ((moveNext = current.MoveNext()) || directories.Count > 0)
            {
                while (!moveNext)
                {
                    current.Dispose();

                    if (directories.Count == 0)
                    {
                        break;
                    }

                    current = directories.Pop();
                    moveNext = current.MoveNext();
                }

                if (!moveNext)
                {
                    break;
                }

                if (rx.IsMatch(current.Current.Key))
                {
                    yield return current.Current.Value.FullPath;
                }

                directories.Push(current);
                current = current.Current.Value.Directories.GetEnumerator();
            }
        }

        public IEnumerable<string> EnumerateFiles(string path, string pattern, SearchOption searchOption)
        {
            if (!IsPathInCone(path, out string processedPath))
            {
                //TODO: Handle cases where part of the directory set is inside our cone and part is not
                foreach (string s in _basis.EnumerateFiles(path, pattern, searchOption))
                {
                    yield return s;
                }

                yield break;
            }

            path = processedPath;
            string rel = path.Substring(_root.FullPath.Length).Trim('/', '\\');
            FileSystemDirectory currentDir = _root;

            if (!string.IsNullOrEmpty(rel))
            {
                string[] parts = rel.Split('/', '\\');
                for (int i = 0; i < parts.Length; ++i)
                {
                    FileSystemDirectory dir;
                    if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                    {
                        yield break;
                    }

                    currentDir = dir;
                }
            }

            Regex rx = new Regex(Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", "."));
            foreach (KeyValuePair<string, FileSystemFile> entry in currentDir.Files)
            {
                if (rx.IsMatch(entry.Key))
                {
                    yield return entry.Value.FullPath;
                }
            }

            if (searchOption == SearchOption.TopDirectoryOnly)
            {
                yield break;
            }

            Stack<IEnumerator<KeyValuePair<string, FileSystemDirectory>>> directories = new Stack<IEnumerator<KeyValuePair<string, FileSystemDirectory>>>();
            Stack<FileSystemDirectory> parentDirectories = new Stack<FileSystemDirectory>();
            IEnumerator<KeyValuePair<string, FileSystemDirectory>> current = currentDir.Directories.GetEnumerator();
            bool moveNext;

            while ((moveNext = current.MoveNext()) || directories.Count > 0)
            {
                while (!moveNext)
                {
                    current.Dispose();

                    if (directories.Count == 0)
                    {
                        break;
                    }

                    current = directories.Pop();
                    moveNext = current.MoveNext();
                }

                if (!moveNext)
                {
                    break;
                }

                foreach (KeyValuePair<string, FileSystemFile> entry in current.Current.Value.Files)
                {
                    if (rx.IsMatch(entry.Key))
                    {
                        yield return entry.Value.FullPath;
                    }
                }

                directories.Push(current);
                current = current.Current.Value.Directories.GetEnumerator();
            }
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string directoryName, string pattern, SearchOption searchOption)
        {
            if (!IsPathInCone(directoryName, out string processedPath))
            {
                //TODO: Handle cases where part of the directory set is inside our cone and part is not
                foreach (string s in _basis.EnumerateFileSystemEntries(directoryName, pattern, searchOption))
                {
                    yield return s;
                }

                yield break;
            }

            directoryName = processedPath;
            string rel = directoryName.Substring(_root.FullPath.Length).Trim('/', '\\');
            FileSystemDirectory currentDir = _root;

            if (!string.IsNullOrEmpty(rel))
            {
                string[] parts = rel.Split('/', '\\');
                for (int i = 0; i < parts.Length; ++i)
                {
                    FileSystemDirectory dir;
                    if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                    {
                        yield break;
                    }

                    currentDir = dir;
                }
            }

            Regex rx = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$");

            foreach (KeyValuePair<string, FileSystemFile> entry in currentDir.Files)
            {
                if (rx.IsMatch(entry.Key))
                {
                    yield return entry.Value.FullPath;
                }
            }

            if (searchOption == SearchOption.TopDirectoryOnly)
            {
                foreach (KeyValuePair<string, FileSystemDirectory> entry in currentDir.Directories)
                {
                    if (rx.IsMatch(entry.Key))
                    {
                        yield return entry.Value.FullPath;
                    }
                }
                yield break;
            }

            Stack<IEnumerator<KeyValuePair<string, FileSystemDirectory>>> directories = new Stack<IEnumerator<KeyValuePair<string, FileSystemDirectory>>>();
            IEnumerator<KeyValuePair<string, FileSystemDirectory>> current = currentDir.Directories.GetEnumerator();
            bool moveNext;

            while ((moveNext = current.MoveNext()) || directories.Count > 0)
            {
                while (!moveNext)
                {
                    current.Dispose();

                    if (directories.Count == 0)
                    {
                        break;
                    }

                    current = directories.Pop();
                    moveNext = current.MoveNext();
                }

                if (!moveNext)
                {
                    break;
                }

                if (rx.IsMatch(current.Current.Key))
                {
                    yield return current.Current.Value.FullPath;
                }

                foreach (KeyValuePair<string, FileSystemFile> entry in current.Current.Value.Files)
                {
                    if (rx.IsMatch(entry.Key))
                    {
                        yield return entry.Value.FullPath;
                    }
                }

                directories.Push(current);
                current = current.Current.Value.Directories.GetEnumerator();
            }
        }

        public void FileCopy(string sourcePath, string targetPath, bool overwrite)
        {
            if (!overwrite && FileExists(targetPath))
            {
                throw new IOException($"File already exists {targetPath}");
            }

            using (Stream s = OpenRead(sourcePath))
            using (Stream t = CreateFile(targetPath))
            {
                s.CopyTo(t);
            }
        }

        public void FileDelete(string path)
        {
            if (!IsPathInCone(path, out string processedPath))
            {
                _basis.FileDelete(path);
                return;
            }

            path = processedPath;
            string rel = path.Substring(_root.FullPath.Length).Trim('/', '\\');
            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    dir = new FileSystemDirectory(parts[i], Path.Combine(currentDir.FullPath, parts[i]));
                    currentDir.Directories[parts[i]] = dir;
                }

                currentDir = dir;
            }

            currentDir.Files.Remove(parts[parts.Length - 1]);
        }

        public bool FileExists(string file)
        {
            if (!IsPathInCone(file, out string processedPath))
            {
                return _basis.FileExists(file);
            }

            file = processedPath;
            string rel = file.Substring(_root.FullPath.Length).Trim('/', '\\');
            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    return false;
                }

                currentDir = dir;
            }

            return currentDir.Files.ContainsKey(parts[parts.Length - 1]);
        }

        public string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }

        public Stream OpenRead(string path)
        {
            if (!IsPathInCone(path, out string processedPath))
            {
                return _basis.OpenRead(path);
            }

            path = processedPath;
            string rel = path.Substring(_root.FullPath.Length).Trim('/', '\\');
            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    dir = new FileSystemDirectory(parts[i], Path.Combine(currentDir.FullPath, parts[i]));
                    currentDir.Directories[parts[i]] = dir;
                }

                currentDir = dir;
            }

            FileSystemFile targetFile;
            if (!currentDir.Files.TryGetValue(parts[parts.Length - 1], out targetFile))
            {
                throw new FileNotFoundException("File not found", path);
            }

            return targetFile.OpenRead();
        }

        public string ReadAllText(string path)
        {
            using (Stream s = OpenRead(path))
            using (StreamReader r = new StreamReader(s, Encoding.UTF8, true, 8192, true))
            {
                return r.ReadToEnd();
            }
        }

        public void WriteAllText(string path, string value)
        {
            using (Stream s = CreateFile(path))
            using (StreamWriter r = new StreamWriter(s, Encoding.UTF8, 8192, true))
            {
                r.Write(value);
            }
        }

        private bool IsPathInCone(string path, out string processedPath)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(GetCurrentDirectory(), path);
            }

            path = path.Replace('\\', '/');

            bool leadSlash = path[0] == '/';

            if (leadSlash)
            {
                path = path.Substring(1);
            }

            string[] parts = path.Split('/');

            List<string> realParts = new List<string>();

            for (int i = 0; i < parts.Length; ++i)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                switch (parts[i])
                {
                    case ".":
                        continue;
                    case "..":
                        realParts.RemoveAt(realParts.Count - 1);
                        break;
                    default:
                        realParts.Add(parts[i]);
                        break;
                }
            }

            if (leadSlash)
            {
                realParts.Insert(0, "");
            }

            processedPath = string.Join(Path.DirectorySeparatorChar + "", realParts);
            if (processedPath.Equals(_root.FullPath) || processedPath.StartsWith(_root.FullPath.TrimEnd('/', '\\') + Path.DirectorySeparatorChar))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public FileAttributes GetFileAttributes(string file)
        {
            if (!IsPathInCone(file, out string processedPath))
            {
                return _basis.GetFileAttributes(file);
            }

            file = processedPath;
            string rel = file.Substring(_root.FullPath.Length).Trim('/', '\\');
            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    dir = new FileSystemDirectory(parts[i], Path.Combine(currentDir.FullPath, parts[i]));
                    currentDir.Directories[parts[i]] = dir;
                }

                currentDir = dir;
            }

            FileSystemFile targetFile;
            if (!currentDir.Files.TryGetValue(parts[parts.Length - 1], out targetFile))
            {
                throw new FileNotFoundException("File not found", file);
            }

            return targetFile.Attributes;
        }

        public void SetFileAttributes(string file, FileAttributes attributes)
        {
            if (!IsPathInCone(file, out string processedPath))
            {
                _basis.SetFileAttributes(file, attributes);
                return;
            }

            file = processedPath;
            string rel = file.Substring(_root.FullPath.Length).Trim('/', '\\');
            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    dir = new FileSystemDirectory(parts[i], Path.Combine(currentDir.FullPath, parts[i]));
                    currentDir.Directories[parts[i]] = dir;
                }

                currentDir = dir;
            }

            FileSystemFile targetFile;
            if (!currentDir.Files.TryGetValue(parts[parts.Length - 1], out targetFile))
            {
                throw new FileNotFoundException("File not found", file);
            }

            targetFile.Attributes = attributes;
        }

        
        public DateTime GetLastWriteTimeUtc(string file)
        {
            if (!IsPathInCone(file, out string processedPath))
            {
                if (_basis is IFileLastWriteTimeSource lastWriteTimeSource)
                {
                    return lastWriteTimeSource.GetLastWriteTimeUtc(file);
                }
                throw new NotImplementedException($"Basis file system must implement {nameof(IFileLastWriteTimeSource)}");
            }

            file = processedPath;
            string rel = file.Substring(_root.FullPath.Length).Trim('/', '\\');
            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    dir = new FileSystemDirectory(parts[i], Path.Combine(currentDir.FullPath, parts[i]));
                    currentDir.Directories[parts[i]] = dir;
                }

                currentDir = dir;
            }

            FileSystemFile targetFile;
            if (!currentDir.Files.TryGetValue(parts[parts.Length - 1], out targetFile))
            {
                throw new FileNotFoundException("File not found", file);
            }

            return targetFile.LastWriteTimeUtc;
        }

        public void SetLastWriteTimeUtc(string file, DateTime lastWriteTimeUtc)
        {
            if (!IsPathInCone(file, out string processedPath))
            {
                if (_basis is IFileLastWriteTimeSource lastWriteTimeSource)
                {
                    lastWriteTimeSource.SetLastWriteTimeUtc(file, lastWriteTimeUtc);
                }
                throw new NotImplementedException($"Basis file system must implement {nameof(IFileLastWriteTimeSource)}");
            }

            file = processedPath;
            string rel = file.Substring(_root.FullPath.Length).Trim('/', '\\');
            string[] parts = rel.Split('/', '\\');
            FileSystemDirectory currentDir = _root;

            for (int i = 0; i < parts.Length - 1; ++i)
            {
                FileSystemDirectory dir;
                if (!currentDir.Directories.TryGetValue(parts[i], out dir))
                {
                    dir = new FileSystemDirectory(parts[i], Path.Combine(currentDir.FullPath, parts[i]));
                    currentDir.Directories[parts[i]] = dir;
                }

                currentDir = dir;
            }

            FileSystemFile targetFile;
            if (!currentDir.Files.TryGetValue(parts[parts.Length - 1], out targetFile))
            {
                throw new FileNotFoundException("File not found", file);
            }

            targetFile.LastWriteTimeUtc = lastWriteTimeUtc;
        }
    }
}

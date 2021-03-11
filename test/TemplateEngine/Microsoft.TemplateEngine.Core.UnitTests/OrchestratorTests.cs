using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class MockFileStream : MemoryStream
    {
        private readonly Action<byte[]> _onFlush;

        public MockFileStream(Action<byte[]> onFlush)
        {
            _onFlush = onFlush;
        }

        public override void Flush()
        {
            byte[] buffer = new byte[Length];
            Read(buffer, 0, buffer.Length);
            _onFlush(buffer);
        }
    }

    public class MockFileSystem : IPhysicalFileSystem
    {
        private HashSet<string> _directories = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public MockFileSystem Add(string filePath, string contents, Encoding encoding = null)
        {
            _files[filePath] = (encoding ?? Encoding.UTF8).GetBytes(contents);

            string currentParent = Path.GetDirectoryName(filePath);

            while (currentParent.IndexOfAny(new[] { '\\', '/' }) != currentParent.Length - 1)
            {
                _directories.Add(currentParent);
                currentParent = Path.GetDirectoryName(filePath);
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
            _files[path] = new byte[0];
            return new MockFileStream(d => _files[path] = d);
        }

        public Stream OpenRead(string path)
        {
            if (!_files.TryGetValue(path, out byte[] contents))
            {
                throw new Exception($"File {path} does no texist");
            }

            MemoryStream s = new MemoryStream(contents);
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
            _files[path] = Encoding.UTF8.GetBytes(value);
        }

        public string ReadAllText(string path)
        {
            return Encoding.UTF8.GetString(_files[path]);
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

            _files[targetPath] = _files[sourcePath];
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string directoryName, string pattern, SearchOption searchOption)
        {
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

        public FileAttributes GetFileAttributes(string file)
        {
            throw new NotImplementedException();
        }

        public void SetFileAttributes(string file, FileAttributes attributes)
        {
            throw new NotImplementedException();
        }
    }

    public class MockGlobalRunSpec : IGlobalRunSpec
    {
        public IReadOnlyList<IPathMatcher> Exclude => new List<IPathMatcher>();

        public IReadOnlyList<IPathMatcher> Include => new List<IPathMatcher>();

        public IReadOnlyList<IPathMatcher> CopyOnly => new List<IPathMatcher>();

        public IReadOnlyList<IOperationProvider> Operations { get; set; }

        public IVariableCollection RootVariableCollection { get; set; }

        public IReadOnlyList<KeyValuePair<IPathMatcher, IRunSpec>> Special { get; set; }

        public IReadOnlyDictionary<string, IReadOnlyList<IOperationProvider>> LocalizationOperations { get; set; }

        public string PlaceholderFilename { get; set; }

        public Dictionary<string, string> TargetRelativePaths { get; set; }

        public IReadOnlyList<string> IgnoreFileNames => new List<string>();

        public bool TryGetTargetRelPath(string sourceRelPath, out string targetRelPath)
        {
            return TargetRelativePaths.TryGetValue(sourceRelPath, out targetRelPath);
        }
    }

    public class MockMountPoint : IMountPoint
    {
        public MockMountPoint()
        {
            MockRoot = new MockDirectory("/", "/", this, null);
        }

        public MountPointInfo Info { get; set; }

        public IDirectory Root => MockRoot;

        public MockDirectory MockRoot { get; }

        public IEngineEnvironmentSettings EnvironmentSettings { get; set; }

        public IFileSystemInfo FileSystemInfo(string fullPath)
        {
            string[] parts = fullPath.TrimStart('/').Split('/');

            IDirectory current = Root;

            for (int i = 0; i < parts.Length; ++i)
            {
                IFileSystemInfo info = current.EnumerateFileSystemInfos(parts[i], SearchOption.TopDirectoryOnly).FirstOrDefault();

                if (info == null)
                {
                    return new MockFile(fullPath, this);
                }

                IDirectory dir = info as IDirectory;

                if (dir != null)
                {
                    current = dir;
                    continue;
                }

                IFile file = info as IFile;

                if (file != null)
                {
                    if (i == parts.Length - 1)
                    {
                        return file;
                    }

                    return new MockFile(fullPath, this);
                }
            }

            return current;
        }

        public IDirectory DirectoryInfo(string fullPath)
        {
            IFileSystemInfo info = FileInfo(fullPath);
            IDirectory resultDir = info as IDirectory;

            if (resultDir != null)
            {
                return resultDir;
            }

            return new MockDirectory(fullPath, this);
        }

        public IFile FileInfo(string fullPath)
        {
            IFileSystemInfo info = FileInfo(fullPath);
            IFile resultFile = info as IFile;

            if (resultFile != null)
            {
                return resultFile;
            }

            return new MockFile(fullPath, this);
        }
    }

    public class MockDirectory : IDirectory
    {
        public MockDirectory(string fullPath, IMountPoint mountPoint)
        {
            if (fullPath[fullPath.Length - 1] != '/')
            {
                fullPath += '/';
            }

            FullPath = fullPath;
            Name = fullPath.Trim('/').Split('/').Last();
            MountPoint = mountPoint;
            Exists = false;
        }

        public MockDirectory(string fullPath, string name, IMountPoint mountPoint, IDirectory parent)
        {
            if (fullPath[fullPath.Length - 1] != '/')
            {
                fullPath += '/';
            }

            FullPath = fullPath;
            Name = name;
            _children = new List<IFileSystemInfo>();
            Exists = true;
            Parent = parent;
            MountPoint = mountPoint;
        }

        private readonly List<IFileSystemInfo> _children;

        public bool Exists { get; }

        public string FullPath { get; }

        public FileSystemInfoKind Kind => FileSystemInfoKind.Directory;

        public IDirectory Parent { get; }

        public string Name { get; }

        public IMountPoint MountPoint { get; }

        public IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string patten, SearchOption searchOption)
        {
            foreach (IFileSystemInfo child in _children)
            {
                yield return child;

                if (searchOption == SearchOption.AllDirectories)
                {
                    IDirectory childDir = child as IDirectory;

                    if (childDir != null)
                    {
                        foreach (IFileSystemInfo nestedChild in childDir.EnumerateFileSystemInfos(patten, searchOption))
                        {
                            yield return nestedChild;
                        }
                    }
                }
            }
        }

        public IEnumerable<IFile> EnumerateFiles(string pattern, SearchOption searchOption)
        {
            foreach (IFileSystemInfo child in _children)
            {
                IFile childFile = child as IFile;
                if (childFile != null)
                {
                    yield return childFile;
                }
                else if (searchOption == SearchOption.AllDirectories)
                {
                    IDirectory childDir = child as IDirectory;

                    if (childDir != null)
                    {
                        foreach (IFileSystemInfo nestedChild in childDir.EnumerateFileSystemInfos(pattern, searchOption))
                        {
                            childFile = nestedChild as IFile;

                            if (childFile != null)
                            {
                                yield return childFile;
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<IDirectory> EnumerateDirectories(string pattern, SearchOption searchOption)
        {
            foreach (IFileSystemInfo child in _children)
            {
                IDirectory childDir = child as IDirectory;
                if (childDir != null)
                {
                    yield return childDir;

                    if (searchOption == SearchOption.AllDirectories)
                    {
                        foreach (IFileSystemInfo nestedChild in childDir.EnumerateFileSystemInfos(pattern, searchOption))
                        {
                            childDir = nestedChild as IDirectory;
                            if (childDir != null)
                            {
                                yield return childDir;
                            }
                        }
                    }
                }
            }
        }

        public MockDirectory AddDirectory(string name)
        {
            MockDirectory dir = new MockDirectory(FullPath + name, name, MountPoint, this);
            _children.Add(dir);
            return dir;
        }

        public MockDirectory AddFile(string name, byte[] contents)
        {
            MockFile file = new MockFile(this, name, MountPoint, contents);
            _children.Add(file);
            return this;
        }
    }

    public class MockFile : IFile
    {
        public MockFile(string fullpath, IMountPoint mountPoint)
        {
            FullPath = fullpath;
            Name = fullpath.Trim('/').Split('/').Last();
            MountPoint = mountPoint;
            Exists = false;
        }

        private readonly byte[] _contents;

        public MockFile(IDirectory parent, string name, IMountPoint mountPoint, byte[] contents)
        {
            FullPath = parent.FullPath + name;
            Name = name;
            MountPoint = mountPoint;
            _contents = contents;
            Parent = parent;
            Exists = true;
        }

        public bool Exists { get; }

        public string FullPath { get; }

        public FileSystemInfoKind Kind => FileSystemInfoKind.File;

        public IDirectory Parent { get; }

        public string Name { get; }

        public IMountPoint MountPoint { get; }

        public Stream OpenRead()
        {
            return new MemoryStream(_contents);
        }
    }

    public class OrchestratorTests
    {
        [Fact(DisplayName = nameof(VerifyRun))]
        public void VerifyRun()
        {
            TestHost host = new TestHost
            {
                HostIdentifier = "TestRunner",
                Version = "1.0.0.0",
            };

            host.FileSystem = new MockFileSystem();

            MockMountPoint mnt = new MockMountPoint();
            mnt.EnvironmentSettings = new EngineEnvironmentSettings(host, x => null);
            mnt.MockRoot.AddDirectory("subdir").AddFile("test.file", new byte[0]);
            Orchestrator orchestrator = new Orchestrator();
            orchestrator.Run(new MockGlobalRunSpec(), mnt.Root, @"c:\temp");
        }
    }
}

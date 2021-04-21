// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockDirectory : IDirectory
    {
        private readonly List<IFileSystemInfo> _children;

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
}

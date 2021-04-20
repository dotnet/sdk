// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Edge.Mount
{
    public abstract class FileSystemInfoBase : IFileSystemInfo
    {
        private IDirectory _parent;

        protected FileSystemInfoBase(IMountPoint mountPoint, string fullPath, string name, FileSystemInfoKind kind)
        {
            FullPath = fullPath.Replace('\\', '/');
            Name = name;
            Kind = kind;
            MountPoint = mountPoint;
        }

        public abstract bool Exists { get; }

        public string FullPath { get; }

        public FileSystemInfoKind Kind { get; }

        public virtual IDirectory Parent
        {
            get
            {
                if (string.Equals(FullPath, "/", StringComparison.Ordinal))
                {
                    return null;
                }

                if (string.Equals(FullPath, $"/{Name}", StringComparison.Ordinal))
                {
                    return MountPoint.Root;
                }

                if (_parent == null)
                {
                    if (FullPath == "/" || FullPath.Length < 2)
                    {
                        return null;
                    }

                    int lastSlash = FullPath.LastIndexOf('/', FullPath.Length - 2);

                    if (lastSlash < 0)
                    {
                        return null;
                    }

                    string parentPath = FullPath.Substring(0, lastSlash + 1);
                    _parent = MountPoint.DirectoryInfo(parentPath);
                }

                return (_parent?.Exists ?? false) ? _parent : null;
            }
        }

        public string Name { get; }

        public IMountPoint MountPoint { get; }
    }
}

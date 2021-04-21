// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockFile : IFile
    {
        private readonly byte[] _contents;

        public MockFile(string fullpath, IMountPoint mountPoint)
        {
            FullPath = fullpath;
            Name = fullpath.Trim('/').Split('/').Last();
            MountPoint = mountPoint;
            Exists = false;
        }

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
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockMountPoint : IMountPoint
    {
        public MockMountPoint(IEngineEnvironmentSettings environmentSettings)
        {
            EnvironmentSettings = environmentSettings;
            MockRoot = new MockDirectory("/", "/", this, null);
            MountPointUri = null!;
        }

        public IDirectory Root => MockRoot;

        public MockDirectory MockRoot { get; }

        public IEngineEnvironmentSettings EnvironmentSettings { get; set; }

        public string MountPointUri { get; }

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

                if (info is IDirectory dir)
                {
                    current = dir;
                    continue;
                }

                if (info is IFile file)
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

            if (info is IDirectory resultDir)
            {
                return resultDir;
            }

            return new MockDirectory(fullPath, this);
        }

        public IFile FileInfo(string fullPath)
        {
            IFileSystemInfo info = FileSystemInfo(fullPath);

            if (info is IFile resultFile)
            {
                return resultFile;
            }

            return new MockFile(fullPath, this);
        }

        public void Dispose()
        {
        }
    }
}

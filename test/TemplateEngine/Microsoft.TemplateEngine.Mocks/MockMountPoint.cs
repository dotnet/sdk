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
            IFileSystemInfo info = FileSystemInfo(fullPath);
            IFile resultFile = info as IFile;

            if (resultFile != null)
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

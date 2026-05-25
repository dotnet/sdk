// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class TestDirectory
    {
        internal TestDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(nameof(path));
            }

            Path = path;

            EnsureExistsAndEmpty(Path);
        }

        public static TestDirectory Create(string path)
        {
            return new TestDirectory(path);
        }

        public string Path { get; private set; }

        private static void EnsureExistsAndEmpty(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    //  Clear read-only flags on anything in the directory
                    var dirInfo = new DirectoryInfo(path)
                    {
                        Attributes = FileAttributes.Normal
                    };
                    foreach (var info in dirInfo.GetFileSystemInfos("*", SearchOption.AllDirectories))
                    {
                        info.Attributes = FileAttributes.Normal;
                    }

                    Directory.Delete(path, true);
                }
                catch (IOException ex)
                {
                    throw new IOException("Unable to delete directory " + path, ex);
                }
            }

            Directory.CreateDirectory(path);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Assertions
{
    public static class DirectoryInfoExtensions
    {
        public static DirectoryInfoAssertions Should(this DirectoryInfo dir)
        {
            return new DirectoryInfoAssertions(dir);
        }

        /// <summary>
        /// Creates a <see cref="DirectoryInfo"/> for the given name in the directory.
        /// If the directory does not exist, it will not be created.
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="name"></param>
        /// <returns></returns>

        public static DirectoryInfo Sub(this DirectoryInfo dir, string name)
        {
            return new DirectoryInfo(Path.Combine(dir.FullName, name));
        }

        /// <summary>
        /// Creates a <see cref="FileInfo"/> for the given name in the directory.
        /// If the file does not exist, it will not be created.
        /// </summary>
        public static FileInfo File(this DirectoryInfo dir, string name)
        {
            return new FileInfo(Path.Combine(dir.FullName, name));
        }
    }
}

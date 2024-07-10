// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class DirectoryInfoExtensions
    {
        public static DirectoryInfoAssertions Should(this DirectoryInfo dir) => new DirectoryInfoAssertions(dir);

        public static DirectoryInfo Sub(this DirectoryInfo dir, string name) => new DirectoryInfo(Path.Combine(dir.FullName, name));

        public static bool Contains(this DirectoryInfo subject, FileSystemInfo target) => target.FullName.StartsWith(subject.FullName);

        public static DirectoryInfo GetDirectory(this DirectoryInfo subject, params string[] directoryNames) => new DirectoryInfo(Path.Combine(subject.FullName, Path.Combine(directoryNames)));

        public static FileInfo GetFile(this DirectoryInfo subject, string fileName) => new FileInfo(Path.Combine(subject.FullName, fileName));
    }
}

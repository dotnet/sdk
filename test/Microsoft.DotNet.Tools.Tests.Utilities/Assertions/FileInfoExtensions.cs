// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static partial class FileInfoExtensions
    {
        public static FileInfoAssertions Should(this FileInfo file) => new FileInfoAssertions(file);

        public static IDisposable Lock(this FileInfo subject) => new FileInfoLock(subject);

        public static IDisposable NuGetLock(this FileInfo subject) => new FileInfoNuGetLock(subject);

        public static string ReadAllText(this FileInfo subject) => File.ReadAllText(subject.FullName);
    }
}

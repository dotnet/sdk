// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Compression;

namespace Microsoft.DotNet.PackageValidation
{
    internal class Helpers
    {
        public static Stream GetFileStreamFromPackage(string packagePath, string entry)
        {
            FileStream stream = File.OpenRead(packagePath);
            var zipFile = new ZipArchive(stream);
            MemoryStream ms = new MemoryStream();
            zipFile.GetEntry(entry).Open().CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static Stream GetFileStreamFromPackage(MemoryStream packageStream, string entry)
        {
            var zipFile = new ZipArchive(packageStream);
            MemoryStream ms = new MemoryStream();
            zipFile.GetEntry(entry).Open().CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}

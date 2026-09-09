// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.Archive;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class ZipFileFileTests
    {
        [TestMethod]
        public void Length_ReportsUncompressedEntryLength()
        {
            byte[] data = [1, 2, 3, 4, 5];
            using MemoryStream archiveStream = new();
            using (ZipArchive archive = new(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                using Stream entryStream = archive.CreateEntry("test.txt").Open();
                entryStream.Write(data, 0, data.Length);
            }

            archiveStream.Position = 0;
            using ZipArchive readArchive = new(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            using ZipFileMountPoint mountPoint = new(A.Fake<IEngineEnvironmentSettings>(), null, "test.zip", readArchive);
            IFile file = mountPoint.FileInfo("/test.txt");

            Assert.AreEqual(data.Length, ((IKnownLengthFile)file).Length);
            using Stream stream = file.OpenRead();
            byte[] actual = new byte[data.Length];
            Assert.AreEqual(data.Length, stream.Read(actual, 0, actual.Length));
            Assert.AreSequenceEqual(data, actual);
        }
    }
}
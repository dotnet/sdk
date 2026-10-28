// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;
namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    [TestClass]
    public class MetadataCacheTest : SdkTest
    {

        [TestMethod]
        public void GetMetadata_AddsToCache()
        {
            // Arrange
            var directory = TestAssetsManager.CreateTestDirectory();
            var metadataCache = new MetadataCache();
            var assemblyFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            // Act
            var result = metadataCache.GetMetadata(assemblyFilePath);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, metadataCache.Cache.Count);
        }

        [TestMethod]
        public void GetMetadata_UsesCache()
        {
            // Arrange
            var directory = TestAssetsManager.CreateTestDirectory();
            var metadataCache = new MetadataCache();
            var assemblyFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            // Act 1
            var result = metadataCache.GetMetadata(assemblyFilePath);

            // Assert 1
            Assert.IsNotNull(result);
            Assert.AreEqual(1, metadataCache.Cache.Count);

            // Act 2
            var cacheResult = metadataCache.GetMetadata(assemblyFilePath);

            // Assert 2
            Assert.AreSame(result, cacheResult);
            Assert.AreEqual(1, metadataCache.Cache.Count);
        }

        [TestMethod]
        public void GetMetadata_MultipleFiles_ReturnsDifferentResultsAndAddsToCache()
        {
            // Arrange
            var directory = TestAssetsManager.CreateTestDirectory();
            var metadataCache = new MetadataCache();
            var assemblyFilePath1 = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");
            var assemblyFilePath2 = LoaderTestResources.Gamma.WriteToFile(directory.Path, "Gamma.dll");

            // Act
            var result1 = metadataCache.GetMetadata(assemblyFilePath1);
            var result2 = metadataCache.GetMetadata(assemblyFilePath2);

            // Assert
            Assert.AreNotSame(result1, result2);
            Assert.AreEqual(2, metadataCache.Cache.Count);
        }

        [TestMethod]
        public void GetMetadata_ReplacesCache_IfFileTimestampChanged()
        {
            // Arrange
            var directory = TestAssetsManager.CreateTestDirectory();
            var metadataCache = new MetadataCache();
            var assemblyFilePath = LoaderTestResources.Delta.WriteToFile(directory.Path, "Delta.dll");

            // Act 1
            var result = metadataCache.GetMetadata(assemblyFilePath);

            // Assert 1
            Assert.IsNotNull(result);
            var entry = Assert.ContainsSingle(metadataCache.Cache.TestingEnumerable);
            Assert.AreSame(result, entry.Value.Metadata);

            // Act 2
            // Update the timestamp of the file
            File.SetLastWriteTimeUtc(assemblyFilePath, File.GetLastWriteTimeUtc(assemblyFilePath).AddSeconds(1));
            var cacheResult = metadataCache.GetMetadata(assemblyFilePath);

            // Assert 2
            Assert.AreNotSame(result, cacheResult);
            entry = Assert.ContainsSingle(metadataCache.Cache.TestingEnumerable);
            Assert.AreSame(cacheResult, entry.Value.Metadata);
        }
    }
}

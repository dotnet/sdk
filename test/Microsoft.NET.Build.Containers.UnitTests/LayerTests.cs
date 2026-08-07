// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class LayerTests
{
    [TestMethod]
    [DoNotParallelize]
    public void FromDirectory_DeletesTempFileWhenCreationFails()
    {
        DirectoryInfo artifactRoot = Directory.CreateTempSubdirectory();
        DirectoryInfo layerContent = Directory.CreateTempSubdirectory();
        string priorArtifactRoot = ContentStore.ArtifactRoot;

        try
        {
            ContentStore.ArtifactRoot = artifactRoot.FullName;

            Assert.ThrowsExactly<ArgumentException>(() =>
                Layer.FromDirectory(layerContent.FullName, "/app", false, "unsupported"));

            Assert.IsEmpty(Directory.EnumerateFiles(ContentStore.TempPath));
        }
        finally
        {
            ContentStore.ArtifactRoot = priorArtifactRoot;
            artifactRoot.Delete(recursive: true);
            layerContent.Delete(recursive: true);
        }
    }
}

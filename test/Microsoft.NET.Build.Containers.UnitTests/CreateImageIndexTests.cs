// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class CreateImageIndexTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task Invalid_metadata_leaves_index_outputs_empty()
    {
        using CreateImageIndex task = new()
        {
            BuildEngine = new Mock<IBuildEngine>().Object,
            Repository = "test",
            ImageTags = ["latest"],
            ArchiveOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".tar"),
            GeneratedContainers = [new TaskItem("invalid")]
        };

        Assert.IsFalse(await task.ExecuteAsync(TestContext.CancellationToken));
        Assert.IsTrue(task.Log.HasLoggedErrors);
        Assert.AreEqual(string.Empty, task.GeneratedImageIndex);
        Assert.AreEqual(string.Empty, task.GeneratedImageIndexDigest);
    }

    [TestMethod]
    public async Task Cancellation_before_generation_leaves_digest_empty()
    {
        using CreateImageIndex task = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => task.ExecuteAsync(cancellation.Token));
        Assert.AreEqual(string.Empty, task.GeneratedImageIndexDigest);
    }

    [TestMethod]
    public async Task Archive_failure_retains_generated_content_identity()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string parentFile = Path.Combine(directory, "existing-file");
            await File.WriteAllTextAsync(parentFile, "not a directory", TestContext.CancellationToken);
            string archive = Path.Combine(parentFile, "image.tar");
            const string config = """{"architecture":"amd64","os":"linux"}""";
            string manifest = $$"""{"schemaVersion":2,"mediaType":"{{SchemaTypes.OciManifestV1}}","config":{"mediaType":"{{SchemaTypes.OciImageConfigV1}}","digest":"{{DigestUtils.ComputeSha256Digest(config)}}","size":{{Encoding.UTF8.GetByteCount(config)}}},"layers":[]}""";
            TaskItem image = new("linux-x64");
            image.SetMetadata("Configuration", config);
            image.SetMetadata("Manifest", manifest);
            image.SetMetadata("ManifestDigest", DigestUtils.ComputeSha256Digest(manifest));
            image.SetMetadata("ManifestMediaType", SchemaTypes.OciManifestV1);
            using CreateImageIndex task = new()
            {
                BuildEngine = new Mock<IBuildEngine>().Object,
                Repository = "test",
                ImageTags = ["latest"],
                ArchiveOutputPath = archive,
                GeneratedContainers = [image]
            };

            await Assert.ThrowsExactlyAsync<IOException>(() => task.ExecuteAsync(TestContext.CancellationToken));
            Assert.IsFalse(string.IsNullOrEmpty(task.GeneratedImageIndex));
            Assert.AreEqual(DigestUtils.ComputeSha256Digest(task.GeneratedImageIndex), task.GeneratedImageIndexDigest);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

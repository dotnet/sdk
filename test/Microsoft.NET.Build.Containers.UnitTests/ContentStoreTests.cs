// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ContentStoreTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DataRow("sha256:c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")]
    [DataRow("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    public void PathForDescriptor_AcceptsWellFormedDigest(string digest)
    {
        Descriptor descriptor = CreateDescriptorWithDigest(digest);
        string path = ContentStore.PathForDescriptor(descriptor);
        Assert.StartsWith(ContentStore.ContentRoot, path);
    }

    [TestMethod]
    [DataRow("")] // empty string
    [DataRow("sha256:../..\\xyz_not_hex!!")] // non-hex characters
    [DataRow("c5098cc7c2a2ad9bfc66e4c4cb242683a578e9d8f25fd8730b289dd5667916ad")] // missing algorithm prefix
    public void PathForDescriptor_RejectsInvalidDigest(string digest)
    {
        Descriptor descriptor = CreateDescriptorWithDigest(digest);
        Assert.ThrowsExactly<InvalidDigestException>(() =>
            ContentStore.PathForDescriptor(descriptor));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows)]
    [DataRow(true)]
    [DataRow(false)]
    public void Concurrent_commit_verifies_a_destination_held_open_by_another_reader(bool identical)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string destination = Path.Combine(root, "blob");
        string temporary = Path.Combine(root, "download.tmp");
        try
        {
            File.WriteAllText(destination, identical ? "download" : "different");
            File.WriteAllText(temporary, "download");
            using (FileStream reader = File.OpenRead(destination))
            {
                if (identical)
                {
                    ContentStore.CommitBlob(temporary, destination, DigestUtils.ComputeSha256Digest("download"), TestContext.CancellationToken);
                }
                else
                {
                    Assert.ThrowsExactly<InvalidDigestException>(() => ContentStore.CommitBlob(
                        temporary, destination, DigestUtils.ComputeSha256Digest("download"), TestContext.CancellationToken));
                }
                Assert.IsFalse(File.Exists(temporary));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Cancelled_commit_does_not_replace_destination_and_removes_temporary_file()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string temporary = Path.Combine(root, "download.tmp");
        string destination = Path.Combine(root, "blob");
        try
        {
            File.WriteAllText(temporary, "download");
            File.WriteAllText(destination, "original");
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            Assert.ThrowsExactly<OperationCanceledException>(() => ContentStore.CommitBlob(
                temporary, destination, DigestUtils.ComputeSha256Digest("download"), cancellation.Token));
            Assert.AreEqual("original", File.ReadAllText(destination));
            Assert.IsFalse(File.Exists(temporary));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Descriptor CreateDescriptorWithDigest(string digest) =>
        new("application/vnd.oci.image.layer.v1.tar+gzip", digest, 1024);
}

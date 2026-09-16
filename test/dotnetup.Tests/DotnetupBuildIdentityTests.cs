// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Dotnet.BuildIdentity;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

[TestClass]
public class DotnetupBuildIdentityTests
{
    private const string Identity = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void MetadataIdentityDependsOnlyOnFullVersionAndRid()
    {
        const string version = "0.2.0-preview.1.26359.118";
        var identity = BuildIdentityMetadata.Compute(version, "win-x64");
        Assert.AreEqual(64, identity.Length);
        Assert.AreEqual(
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                "dotnetup-version-id-v1\n0.2.0-preview.1.26359.118\nwin-x64"u8)),
            identity);
        Assert.AreEqual(identity, BuildIdentityMetadata.Compute(version, "win-x64"));
        Assert.AreNotEqual(identity, BuildIdentityMetadata.Compute("0.2.0-preview.1.26359.119", "win-x64"));
        Assert.AreNotEqual(identity, BuildIdentityMetadata.Compute(version, "win-arm64"));
        Assert.AreNotEqual(identity, BuildIdentityMetadata.Compute(version + "+revision", "win-x64"));
    }

    [TestMethod]
    public void MetadataIdentityValidatesTheEmbeddedRecord()
    {
        var identity = BuildIdentityMetadata.Compute("0.2.0-dev", "win-x64");
        using var artifact = new MemoryStream(CreateRecord(identity));
        Assert.AreEqual(identity, BuildIdentityMetadata.Validate(artifact, "0.2.0-dev", "win-x64"));
        artifact.Position = 0;
        Assert.ThrowsExactly<InvalidDataException>(() => BuildIdentityMetadata.Validate(artifact, "0.2.1-dev", "win-x64"));
        artifact.Position = 0;
        Assert.ThrowsExactly<InvalidDataException>(() => BuildIdentityMetadata.Validate(artifact, "0.2.0-dev", "linux-x64"));
    }

    [TestMethod]
    [DataRow("", "win-x64")]
    [DataRow("0.2.0-dev", "")]
    [DataRow("0.2.0-dev\nother", "win-x64")]
    public void MetadataIdentityRejectsMissingOrAmbiguousMetadata(string version, string rid)
    {
        Assert.ThrowsExactly<ArgumentException>(() => BuildIdentityMetadata.Compute(version, rid));
    }

    [TestMethod]
    public void ReadFindsUnalignedRecordsAcrossEveryBufferBoundary()
    {
        for (var offset = 4000; offset < 4300; offset++)
        {
            using var stream = new MemoryStream();
            stream.Write(new byte[offset]);
            stream.Write(CreateRecord());
            stream.Write(new byte[9000]);
            stream.Position = 0;
            Assert.AreEqual(Identity, DotnetupBuildIdentityReader.Read(stream));
            Assert.AreEqual(stream.Length, stream.Position);
        }
    }

    [TestMethod]
    public void ReadRejectsMissingDuplicateAndTruncatedRecords()
    {
        using var empty = new MemoryStream();
        ExpectInvalid(empty);
        var record = CreateRecord();
        for (var length = 0; length < record.Length; length++)
        {
            using var truncated = new MemoryStream(record, 0, length);
            ExpectInvalid(truncated);
        }

        using var duplicate = new MemoryStream();
        duplicate.Write(record);
        duplicate.Write(new byte[10000]);
        duplicate.Write(record);
        duplicate.Position = 0;
        ExpectInvalid(duplicate);
    }

    [TestMethod]
    public void ReadRejectsEveryMalformedRecordField()
    {
        for (var offset = 16; offset < 96; offset++)
        {
            var record = CreateRecord();
            record[offset] = 255;
            using var stream = new MemoryStream(record);
            ExpectInvalid(stream);
        }

        var uppercase = CreateRecord();
        uppercase[24] = (byte)'A';
        using var uppercaseStream = new MemoryStream(uppercase);
        ExpectInvalid(uppercaseStream);
    }

    [TestMethod]
    public void ReadSupportsShortNonSeekableReadsAndLeavesStreamOpen()
    {
        for (var maximumRead = 1; maximumRead <= 100; maximumRead++)
        {
            using var stream = new IdentityTestStream(CreateRecord(), maximumRead);
            Assert.AreEqual(Identity, DotnetupBuildIdentityReader.Read(stream));
            Assert.IsTrue(stream.CanRead);
        }
    }

    [TestMethod]
    public void ReadPropagatesFailureEvenAfterFindingARecord()
    {
        using var stream = new IdentityTestStream(CreateRecord(), 7, failAtEnd: true);
        var exception = Assert.ThrowsExactly<IOException>(() => DotnetupBuildIdentityReader.Read(stream));
        Assert.AreEqual("Injected identity read failure.", exception.Message);
    }

    [TestMethod]
    public void ReadRejectsAStreamAlreadyPositionedPastOtherRecords()
    {
        using var stream = new MemoryStream(CreateRecord());
        stream.Position = 1;
        Assert.ThrowsExactly<ArgumentException>(() => DotnetupBuildIdentityReader.Read(stream));
    }

    [TestMethod]
    public void CurrentMatchesTheSingleRecordInTheOwningAssembly()
    {
        using var stream = File.OpenRead(typeof(DotnetupBuildIdentity).Assembly.Location);
        Assert.AreEqual(DotnetupBuildIdentity.Current, DotnetupBuildIdentityReader.Read(stream));
        Assert.AreEqual(64, DotnetupBuildIdentity.Current.Length);
    }

    [TestMethod]
    public void GenerationWritesOnlyChangedBytesAndDoesNotTrustTimestamps()
    {
        var directory = Path.Combine(Path.GetTempPath(), "identity-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "record.cs");
            var bytes = Encoding.UTF8.GetBytes(BuildIdentityMetadata.Source("0.2.0-dev", "win-x64"));
            BuildIdentityMetadata.WriteIfChanged(path, bytes);
            var timestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, timestamp);
            BuildIdentityMetadata.WriteIfChanged(path, bytes);
            Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(path));
            File.WriteAllText(path, "modified");
            File.SetLastWriteTimeUtc(path, timestamp);
            BuildIdentityMetadata.WriteIfChanged(path, bytes);
            Assert.AreSequenceEqual(bytes, File.ReadAllBytes(path));
            Assert.HasCount(1, Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] CreateRecord(string identity = Identity)
    {
        return Encoding.ASCII.GetBytes("DOTNETUP-ID-REC\0\u0001\0\0\0\u0040\0\0\0" + identity + "END-ID\0\0");
    }

    private static void ExpectInvalid(Stream stream)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => DotnetupBuildIdentityReader.Read(stream));
    }
}
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.Dotnet.VersionMetadata;
using Microsoft.DotNet.Tools.Bootstrapper.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

[TestClass]
public class DotnetupVersionMetadataTests
{
    private const string Version = "0.2.0-preview.1.26359.118+revision";
    private const string Rid = "win-x64";
    private const string Metadata = Version + "|" + Rid;

    [TestMethod]
    public void MetadataPreservesFullVersionAndRidWithoutHashing()
    {
        Assert.AreEqual(Metadata, DotnetupVersionMetadataReader.Format(Version, Rid));
        Assert.AreNotEqual(Metadata, DotnetupVersionMetadataReader.Format("0.2.0-preview.1.26359.119", Rid));
        Assert.AreNotEqual(Metadata, DotnetupVersionMetadataReader.Format(Version, "win-arm64"));
        Assert.AreNotEqual(Metadata, DotnetupVersionMetadataReader.Format(Version.Split('+')[0], Rid));
    }

    [TestMethod]
    public void MetadataValidatesTheEmbeddedRecord()
    {
        using var artifact = new MemoryStream(CreateRecord());
        Assert.AreEqual(Metadata, VersionMetadataSource.Validate(artifact, Version, Rid));
        artifact.Position = 0;
        Assert.ThrowsExactly<InvalidDataException>(() => VersionMetadataSource.Validate(artifact, "0.2.1-dev", Rid));
        artifact.Position = 0;
        Assert.ThrowsExactly<InvalidDataException>(() => VersionMetadataSource.Validate(artifact, Version, "linux-x64"));
    }

    [TestMethod]
    [DataRow("", "win-x64")]
    [DataRow("0.2.0-dev", "")]
    [DataRow("0.2.0-dev\nother", "win-x64")]
    [DataRow("0.2.0|other", "win-x64")]
    [DataRow("0.2.0", "win|x64")]
    [DataRow("0.2.0-é", "win-x64")]
    public void MetadataRejectsMissingOrAmbiguousFields(string version, string rid)
    {
        Assert.ThrowsExactly<ArgumentException>(() => DotnetupVersionMetadataReader.Format(version, rid));
    }

    [TestMethod]
    public void MetadataRejectsOversizedFieldsWithoutTruncation()
    {
        var version = new string('1', 223 - Rid.Length - 1);
        using var stream = new MemoryStream(VersionMetadataSource.CreateRecord(version, Rid));
        Assert.AreEqual(version + "|" + Rid, DotnetupVersionMetadataReader.Read(stream));
        Assert.ThrowsExactly<ArgumentException>(() => VersionMetadataSource.CreateRecord(version + "1", Rid));
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
            Assert.AreEqual(Metadata, DotnetupVersionMetadataReader.Read(stream));
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
        for (var offset = 16; offset < DotnetupVersionMetadataReader.RecordLength; offset++)
        {
            var record = CreateRecord();
            record[offset] = 255;
            using var stream = new MemoryStream(record);
            ExpectInvalid(stream);
        }

        var missingSeparator = CreateRecord();
        missingSeparator[24 + Version.Length] = (byte)'A';
        using var malformedStream = new MemoryStream(missingSeparator);
        ExpectInvalid(malformedStream);
    }

    [TestMethod]
    public void ReadSupportsShortNonSeekableReadsAndLeavesStreamOpen()
    {
        for (var maximumRead = 1; maximumRead <= 100; maximumRead++)
        {
            using var stream = new IdentityTestStream(CreateRecord(), maximumRead);
            Assert.AreEqual(Metadata, DotnetupVersionMetadataReader.Read(stream));
            Assert.IsTrue(stream.CanRead);
        }
    }

    [TestMethod]
    public void ReadPropagatesFailureEvenAfterFindingARecord()
    {
        using var stream = new IdentityTestStream(CreateRecord(), 7, failAtEnd: true);
        var exception = Assert.ThrowsExactly<IOException>(() => DotnetupVersionMetadataReader.Read(stream));
        Assert.AreEqual("Injected identity read failure.", exception.Message);
    }

    [TestMethod]
    public void ReadRejectsAStreamAlreadyPositionedPastOtherRecords()
    {
        using var stream = new MemoryStream(CreateRecord());
        stream.Position = 1;
        Assert.ThrowsExactly<ArgumentException>(() => DotnetupVersionMetadataReader.Read(stream));
    }

    [TestMethod]
    public void CurrentMatchesTheSingleRecordInTheOwningAssembly()
    {
        using var stream = File.OpenRead(typeof(DotnetupVersionMetadata).Assembly.Location);
        Assert.AreEqual(DotnetupVersionMetadata.Current, DotnetupVersionMetadataReader.Read(stream));
        Assert.HasCount(2, DotnetupVersionMetadata.Current.Split('|'));
    }

    [TestMethod]
    public void GenerationWritesOnlyChangedBytesAndDoesNotTrustTimestamps()
    {
        var directory = Path.Combine(Path.GetTempPath(), "identity-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "record.cs");
            var bytes = Encoding.UTF8.GetBytes(VersionMetadataSource.Source("0.2.0-dev", "win-x64"));
            VersionMetadataSource.WriteIfChanged(path, bytes);
            var timestamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, timestamp);
            VersionMetadataSource.WriteIfChanged(path, bytes);
            Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(path));
            File.WriteAllText(path, "modified");
            File.SetLastWriteTimeUtc(path, timestamp);
            VersionMetadataSource.WriteIfChanged(path, bytes);
            Assert.AreSequenceEqual(bytes, File.ReadAllBytes(path));
            Assert.HasCount(1, Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] CreateRecord() => VersionMetadataSource.CreateRecord(Version, Rid);

    private static void ExpectInvalid(Stream stream)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => DotnetupVersionMetadataReader.Read(stream));
    }
}
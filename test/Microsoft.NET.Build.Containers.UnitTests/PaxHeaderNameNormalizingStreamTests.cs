// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.Text;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class PaxHeaderNameNormalizingStreamTests
{
    /// <summary>Writes a small pax archive, optionally through the normalizing stream.</summary>
    private static byte[] WriteArchive(bool normalize, int writeChunkSize = int.MaxValue)
    {
        byte[] raw;
        using (var buffer = new MemoryStream())
        {
            using (var writer = new TarWriter(buffer, TarEntryFormat.Pax, leaveOpen: true))
            {
                foreach (string name in new[] { "app", "app/a.txt", "app/nested/deeper/b.txt" })
                {
                    bool isFile = name.EndsWith(".txt", StringComparison.Ordinal);
                    var entry = new PaxTarEntry(
                        isFile ? TarEntryType.RegularFile : TarEntryType.Directory,
                        name,
                        new Dictionary<string, string> { ["custom"] = "value" })
                    {
                        ModificationTime = DateTimeOffset.FromUnixTimeSeconds(1636374896)
                    };

                    if (isFile)
                    {
                        // Content deliberately long enough to span several blocks.
                        entry.DataStream = new MemoryStream(Encoding.ASCII.GetBytes(new string('x', 3000)));
                    }

                    writer.WriteEntry(entry);
                }
            }

            raw = buffer.ToArray();
        }

        if (!normalize)
        {
            return raw;
        }

        using var destination = new MemoryStream();
        using (var stream = new PaxHeaderNameNormalizingStream(destination, leaveOpen: true))
        {
            for (int offset = 0; offset < raw.Length; offset += writeChunkSize)
            {
                int count = Math.Min(writeChunkSize, raw.Length - offset);
                stream.Write(raw, offset, count);
            }
        }

        return destination.ToArray();
    }

    [TestMethod]
    public void RemovesTheProcessIdFromExtendedHeaderNames()
    {
        string text = Encoding.ASCII.GetString(WriteArchive(normalize: true));

        Assert.IsFalse(text.Contains("PaxHeaders.", StringComparison.Ordinal), "The process-dependent name should be gone.");
        Assert.IsTrue(text.Contains("./PaxHeaders/.", StringComparison.Ordinal), "The normalized name should be present.");
    }

    [TestMethod]
    public void ProducesAnArchiveThatStillReadsCorrectly()
    {
        // Rewriting the header must not corrupt the archive: the entries, their metadata and their
        // content all have to survive.
        using var source = new MemoryStream(WriteArchive(normalize: true));
        using var reader = new TarReader(source);

        var names = new List<string>();
        while (reader.GetNextEntry() is TarEntry entry)
        {
            names.Add(entry.Name);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1636374896), entry.ModificationTime);

            if (entry.EntryType == TarEntryType.RegularFile)
            {
                using var content = new StreamReader(entry.DataStream!);
                Assert.AreEqual(new string('x', 3000), content.ReadToEnd());
            }

            Assert.AreEqual("value", ((PaxTarEntry)entry).ExtendedAttributes["custom"]);
        }

        Assert.AreSequenceEqual(new[] { "app", "app/a.txt", "app/nested/deeper/b.txt" }, names);
    }

    [TestMethod]
    public void LeavesTheArchiveTheSameLength()
    {
        Assert.HasCount(WriteArchive(normalize: false).Length, WriteArchive(normalize: true));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(512)]
    [DataRow(513)]
    [DataRow(4096)]
    public void ProducesTheSameResultRegardlessOfHowTheDataIsChunked(int chunkSize)
    {
        // A caller may write across block boundaries, so the stream has to reassemble blocks itself.
        Assert.AreSequenceEqual(
            WriteArchive(normalize: true),
            WriteArchive(normalize: true, writeChunkSize: chunkSize));
    }

    [TestMethod]
    public void DoesNotRewriteFileContentThatLooksLikeAHeader()
    {
        // A header is only recognized where one can occur, so file content that happens to contain
        // the magic bytes must be passed through untouched.
        byte[] content = new byte[512];
        content.AsSpan().Fill((byte)'a');
        "ustar"u8.CopyTo(content.AsSpan(257));
        content[156] = (byte)'x';
        "./PaxHeaders.99999/."u8.CopyTo(content.AsSpan(0));

        byte[] raw;
        using (var buffer = new MemoryStream())
        {
            using (var writer = new TarWriter(buffer, TarEntryFormat.Pax, leaveOpen: true))
            {
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "file")
                {
                    DataStream = new MemoryStream(content)
                });
            }
            raw = buffer.ToArray();
        }

        using var destination = new MemoryStream();
        using (var stream = new PaxHeaderNameNormalizingStream(destination, leaveOpen: true))
        {
            stream.Write(raw, 0, raw.Length);
        }

        using var source = new MemoryStream(destination.ToArray());
        using var reader = new TarReader(source);
        TarEntry entry = reader.GetNextEntry()!;
        using var actual = new MemoryStream();
        entry.DataStream!.CopyTo(actual);

        Assert.AreSequenceEqual(content, actual.ToArray(), "File content must be passed through unchanged.");
    }

    [TestMethod]
    public void ForwardsATrailingPartialBlockRatherThanDroppingIt()
    {
        using var destination = new MemoryStream();
        using (var stream = new PaxHeaderNameNormalizingStream(destination, leaveOpen: true))
        {
            stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
        }

        Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, destination.ToArray());
    }
}

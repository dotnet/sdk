// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
/// Tests the AOT-safe conservative file-directive probe.
/// </summary>
[TestClass]
public class FileBasedAppDirectiveProbeTests
{
    /// <summary>Verifies that sources without directive bytes return <see cref="FileBasedAppDirectiveProbeResult.None"/>.</summary>
    [TestMethod]
    public void ProbeReturnsNoneWhenDirectiveBytesAreAbsent()
    {
        string[] sources =
        [
            string.Empty,
            "Console.WriteLine(42);",
            "#!/usr/bin/env dotnet\nConsole.WriteLine(42);",
            "var text = \"# not a directive\";",
        ];

        foreach (string source in sources)
        {
            Assert.AreEqual(FileBasedAppDirectiveProbeResult.None, Probe(Encoding.UTF8.GetBytes(source)));
        }
    }

    /// <summary>Verifies that possible directive bytes return <see cref="FileBasedAppDirectiveProbeResult.Unknown"/>.</summary>
    [TestMethod]
    public void ProbeReturnsUnknownWhenDirectiveBytesArePresent()
    {
        string[] sources =
        [
            "#:package Example@1.0.0",
            "// #: inside a comment",
            "var text = \"#: inside a string\";",
        ];

        foreach (string source in sources)
        {
            Assert.AreEqual(FileBasedAppDirectiveProbeResult.Unknown, Probe(Encoding.UTF8.GetBytes(source)));
        }
    }

    /// <summary>Verifies that directive bytes split across read buffers are detected.</summary>
    [TestMethod]
    public void ProbeFindsDirectiveBytesAcrossBufferBoundary()
    {
        byte[] bytes = new byte[513];
        Array.Fill(bytes, (byte)' ');
        bytes[511] = (byte)'#';
        bytes[512] = (byte)':';

        Assert.AreEqual(FileBasedAppDirectiveProbeResult.Unknown, Probe(bytes));
    }

    /// <summary>Verifies that unsupported Unicode preambles defer conservatively.</summary>
    [TestMethod]
    public void ProbeTreatsUnsupportedUnicodePreamblesAsUnknown()
    {
        byte[][] preambles =
        [
            [0xFF, 0xFE],
            [0xFE, 0xFF],
            [0xFF, 0xFE, 0x00, 0x00],
            [0x00, 0x00, 0xFE, 0xFF],
        ];

        foreach (byte[] preamble in preambles)
        {
            Assert.AreEqual(FileBasedAppDirectiveProbeResult.Unknown, Probe(preamble));
        }
    }

    /// <summary>Verifies that a UTF-8 preamble does not prevent proving directive absence.</summary>
    [TestMethod]
    public void ProbeAcceptsUtf8PreambleWithoutDirectiveBytes()
    {
        byte[] source = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Console.WriteLine(42);")];

        Assert.AreEqual(FileBasedAppDirectiveProbeResult.None, Probe(source));
    }

    /// <summary>Verifies bounded-buffer scanning for a large source file.</summary>
    [TestMethod]
    public void ProbeScansLargeFileWithBoundedBuffer()
    {
        byte[] source = new byte[10 * 1024 * 1024];
        Array.Fill(source, (byte)' ');

        Assert.AreEqual(FileBasedAppDirectiveProbeResult.None, Probe(source));
    }

    /// <summary>Verifies that source metadata changes during probing return an unknown result.</summary>
    [TestMethod]
    public void ProbeReturnsUnknownWhenSourceMetadataChanges()
    {
        string path = CreatePath();
        try
        {
            File.WriteAllText(path, "Console.WriteLine(42);");

            FileBasedAppDirectiveProbeResult result = FileBasedAppDirectiveProbe.Probe(
                path,
                () => File.AppendAllText(path, Environment.NewLine));

            Assert.AreEqual(FileBasedAppDirectiveProbeResult.Unknown, result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies that source read failures return an unknown result.</summary>
    [TestMethod]
    public void ProbeReturnsUnknownOnReadFailure()
    {
        Assert.AreEqual(
            FileBasedAppDirectiveProbeResult.Unknown,
            FileBasedAppDirectiveProbe.Probe(CreatePath()));
    }

    private static FileBasedAppDirectiveProbeResult Probe(byte[] bytes)
    {
        string path = CreatePath();
        try
        {
            File.WriteAllBytes(path, bytes);
            return FileBasedAppDirectiveProbe.Probe(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreatePath()
        => Path.Join(Path.GetTempPath(), $"dotnet-aot-directive-probe-{Guid.NewGuid():N}.cs");
}

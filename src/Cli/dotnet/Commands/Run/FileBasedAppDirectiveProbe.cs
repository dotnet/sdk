// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Conservatively proves the absence of file-directive byte sequences without rooting or invoking the Roslyn parser.
/// </summary>
/// <remarks>
/// The launch-only path needs only a one-way proof that application directives are absent.
/// Ambiguous input and source changes observed during the scan fall back to the managed CLI.
/// </remarks>
internal static class FileBasedAppDirectiveProbe
{
    private const int BufferSize = 512;

    /// <summary>
    /// Probes a source file for possible file directives.
    /// </summary>
    /// <param name="filePath">The source file path.</param>
    /// <returns><see cref="FileBasedAppDirectiveProbeResult.None"/> only when directive bytes are proven absent; otherwise, <see cref="FileBasedAppDirectiveProbeResult.Unknown"/>.</returns>
    internal static FileBasedAppDirectiveProbeResult Probe(string filePath)
        => Probe(filePath, beforeFinalMetadataCheck: null);

    /// <summary>
    /// Probes a source file and invokes a test hook before validating final file metadata.
    /// </summary>
    /// <param name="filePath">The source file path.</param>
    /// <param name="beforeFinalMetadataCheck">An optional test hook invoked after reading the file.</param>
    /// <returns><see cref="FileBasedAppDirectiveProbeResult.None"/> only when directive bytes are proven absent and file metadata is stable; otherwise, <see cref="FileBasedAppDirectiveProbeResult.Unknown"/>.</returns>
    internal static FileBasedAppDirectiveProbeResult Probe(string filePath, Action? beforeFinalMetadataCheck)
    {
        try
        {
            long initialLength;
            DateTime initialLastWriteTimeUtc;
            long scannedLength = 0;
            bool previousWasHash = false;

            using (var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: BufferSize,
                FileOptions.SequentialScan))
            {
                var initialInfo = new FileInfo(filePath);
                initialInfo.Refresh();
                if (!initialInfo.Exists)
                {
                    return FileBasedAppDirectiveProbeResult.Unknown;
                }

                initialLength = stream.Length;
                initialLastWriteTimeUtc = initialInfo.LastWriteTimeUtc;
                Span<byte> buffer = stackalloc byte[BufferSize];
                bool firstBuffer = true;
                int bytesRead;
                while ((bytesRead = stream.Read(buffer)) != 0)
                {
                    ReadOnlySpan<byte> bytes = buffer[..bytesRead];
                    if (firstBuffer)
                    {
                        firstBuffer = false;
                        if (HasUtf16OrUtf32Preamble(bytes))
                        {
                            return FileBasedAppDirectiveProbeResult.Unknown;
                        }
                    }

                    // In UTF-8, ASCII bytes cannot occur inside a multi-byte sequence. Searching for
                    // the literal '#' ':' bytes is therefore equivalent to a text search without
                    // decoding or allocating the source. BOM-tagged UTF-16 and UTF-32 defer above.
                    foreach (byte value in bytes)
                    {
                        if (previousWasHash && value == (byte)':')
                        {
                            return FileBasedAppDirectiveProbeResult.Unknown;
                        }

                        previousWasHash = value == (byte)'#';
                    }

                    scannedLength += bytesRead;
                }
            }

            beforeFinalMetadataCheck?.Invoke();

            var finalInfo = new FileInfo(filePath);
            finalInfo.Refresh();
            // A concurrent save can otherwise make a stale read prove absence immediately after
            // directives were added. Any observed metadata change makes the result inconclusive.
            if (!finalInfo.Exists ||
                scannedLength != initialLength ||
                finalInfo.Length != initialLength ||
                finalInfo.LastWriteTimeUtc != initialLastWriteTimeUtc)
            {
                return FileBasedAppDirectiveProbeResult.Unknown;
            }

            return FileBasedAppDirectiveProbeResult.None;
        }
        catch (Exception exception) when (
            // File and path failures make the probe inconclusive. Do not hide resource exhaustion
            // or programming errors by catching every exception.
            exception is IOException or
            UnauthorizedAccessException or
            SecurityException or
            NotSupportedException or
            ArgumentException)
        {
            return FileBasedAppDirectiveProbeResult.Unknown;
        }
    }

    private static bool HasUtf16OrUtf32Preamble(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 2 &&
            ((bytes[0] == 0xFF && bytes[1] == 0xFE) ||
             (bytes[0] == 0xFE && bytes[1] == 0xFF)))
        {
            return true;
        }

        return bytes.Length >= 4 &&
            bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF;
    }
}

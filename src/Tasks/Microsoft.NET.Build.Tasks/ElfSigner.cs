// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// OpenHarmony ELF64 self-signing algorithm, ported from ohos-bst-light's selfsign (0BSD).
    /// Algorithm: SHA-256 page-based Merkle root over the file with the .codesign section zeroed,
    /// plus a fixed 256-byte fs-verity-style descriptor and a 32-byte SHA-256 signature, written
    /// into a 4KB .codesign section appended to the file.
    ///
    /// Data preservation is a hard invariant: signing only rewrites the .codesign payload itself
    /// (in place, when the existing block has the layout this signer produces) or appends a new
    /// section after the original bytes, and never drops data that follows the signature block
    /// (e.g. a SingleFile bundle appended to the apphost by the bundler). Malformed section
    /// layouts are refused (fail-closed) instead of being silently "repaired", and foreign
    /// signatures are only replaced with the explicit force opt-in.
    /// </summary>
    internal static class ElfSigner
    {
        private const int DescSize = 256;
        private const int PageSize = 4096;
        private const uint FlagSelfSign = 0x10;
        private const uint FsVerityDescriptorType = 1;
        private const int HashOut = 32;
        private const int PayloadSize = 8 + DescSize + HashOut;

        // ELF64 header field offsets
        private const int EShOff = 0x28;
        private const int EShentsize = 0x3a;
        private const int EShnum = 0x3c;
        private const int EShstrndx = 0x3e;

        // ".codesign\0" including the trailing NUL (10 bytes)
        private static readonly byte[] s_codesignName = new byte[] { (byte)'.', (byte)'c', (byte)'o', (byte)'d', (byte)'e', (byte)'s', (byte)'i', (byte)'g', (byte)'n', 0 };

        /// <summary>
        /// Extensions of artifact formats that cannot carry executable ELF content: .hap,
        /// .nupkg and .zip are zip containers, .pdb is an MSF debug container and
        /// .json/.xml/.txt are text metadata. These make up the bulk of a build output tree
        /// (of the measured 532-file MAUI output only 26 files are ELF), so they are the
        /// files that benefit from the 64-byte header probe below.
        ///
        /// The list is diagnostic only: <see cref="SignFileInPlace"/> never skips a file
        /// because of its extension. Every file's first 64 bytes are probed for the ELF64
        /// magic, so an ELF that happens to be named .hap is still signed (signing gates
        /// must not silently drop a misnamed executable) and a text file named .so is still
        /// skipped without being read in full.
        /// </summary>
        private static readonly string[] s_nonExecutableExtensions =
        {
            ".hap", ".zip", ".nupkg", ".pdb", ".json", ".xml", ".txt",
        };

        /// <summary>
        /// True when <paramref name="path"/> has an extension that is known not to be an
        /// executable format. Used to describe skipped files in logs; never used to decide
        /// whether a file is signed (see <see cref="s_nonExecutableExtensions"/>).
        /// </summary>
        internal static bool HasNonExecutableExtension(string path)
        {
            foreach (string extension in s_nonExecutableExtensions)
            {
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Result of an in-place signing request.</summary>
        public enum SignOutcome
        {
            /// <summary>The path is not an ELF64 file (or does not exist); nothing was written.</summary>
            NotElf,

            /// <summary>A valid .codesign section is already present; the file was left untouched.</summary>
            AlreadyValid,

            /// <summary>The file was signed (or re-signed) and written back.</summary>
            Signed,

            /// <summary>
            /// An existing .codesign section that this signer did not produce (a foreign or
            /// non-self-sign signature) was left untouched. Replacing it requires the explicit
            /// force opt-in so a build can never silently rewrite someone else's signature.
            /// </summary>
            ForeignSignatureRetained,
        }

        /// <summary>How an existing .codesign section relates to this signer.</summary>
        private enum CodesignState
        {
            /// <summary>No .codesign section.</summary>
            None,

            /// <summary>A well-formed self-signature that matches the current content.</summary>
            ValidSelfSign,

            /// <summary>A well-formed self-signature that no longer matches the content.</summary>
            StaleSelfSign,

            /// <summary>A well-formed signature that this signer did not produce.</summary>
            Foreign,

            /// <summary>Inconsistent section layout; refuses to sign (fail-closed).</summary>
            Malformed,
        }

        /// <summary>
        /// Signs an ELF64 file in place. When <paramref name="force"/> is false and the file already
        /// carries a valid self-signature (verified by recomputing the page Merkle root and the
        /// descriptor digest), the file is left untouched so repeated builds do not rewrite it.
        /// A foreign .codesign section is also left untouched unless <paramref name="force"/> is
        /// set. Re-signing never drops data that follows the signature block.
        ///
        /// Cost contract: only the 64-byte ELF header is read before the file is committed to
        /// being an ELF, and an already-valid signature is verified by streaming the file page
        /// by page. The body is materialized only when the signature must actually be (re)written,
        /// so a 532-file/171MB output tree (26 ELF/30MB, one 32MB .hap) never loads .hap, .pdb
        /// or .json bodies, and never loads an up-to-date ELF into the LOH.
        /// </summary>
        public static SignOutcome SignFileInPlace(string path, bool force = false)
        {
            if (!File.Exists(path))
            {
                return SignOutcome.NotElf;
            }

            // Probe only the header first: formats such as .hap/.zip/.nupkg (containers),
            // .pdb (MSF) and .json/.xml/.txt (text) are rejected here after 64 bytes instead
            // of being read whole. The extension is not consulted: only the magic decides, so
            // no ELF is ever skipped because of its name.
            // Reads are unbuffered (bufferSize 1): the access pattern is small scattered
            // reads plus 4KB signature pages, and a read-ahead buffer would pull up to
            // 64KB per probe for the ~500 files that are rejected from their header.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1))
            {
                byte[] header = new byte[64];
                if (ReadAtMost(stream, header, header.Length) < header.Length || !IsElf64(header))
                {
                    return SignOutcome.NotElf;
                }

                if (!force)
                {
                    // Streamed validation: an unchanged, validly signed ELF (the repeat-build
                    // case) is verified without materializing the body.
                    CodesignState state = ClassifyCodesign(ElfImage.FromStream(stream), out _, out _, out _, out string? anomaly);
                    if (state == CodesignState.Malformed)
                    {
                        throw new InvalidDataException($"refusing to sign: {anomaly}");
                    }

                    if (state == CodesignState.ValidSelfSign)
                    {
                        return SignOutcome.AlreadyValid;
                    }

                    if (state == CodesignState.Foreign)
                    {
                        return SignOutcome.ForeignSignatureRetained;
                    }
                }
            }

            // The signature must be written (or force was requested): load the bytes and reuse
            // the in-memory path, which re-parses and re-validates everything before writing.
            byte[] raw = File.ReadAllBytes(path);
            if (!IsElf64(raw))
            {
                // The file changed between the probe and the read; fail closed as "not an ELF"
                // rather than signing bytes that were never classified.
                return SignOutcome.NotElf;
            }

            CodesignState memoryState = ClassifyCodesign(raw, out _, out _, out _, out string? memoryAnomaly);
            if (memoryState == CodesignState.Malformed)
            {
                throw new InvalidDataException($"refusing to sign: {memoryAnomaly}");
            }

            if (memoryState == CodesignState.ValidSelfSign && !force)
            {
                return SignOutcome.AlreadyValid;
            }

            if (memoryState == CodesignState.Foreign && !force)
            {
                return SignOutcome.ForeignSignatureRetained;
            }

            byte[] signed = SignElf(raw, force: true);
            // FileMode.Create truncates the existing inode in place, preserving its Unix permissions.
            File.WriteAllBytes(path, signed);
            return SignOutcome.Signed;
        }

        /// <summary>Reads up to <paramref name="count"/> bytes, tolerating short reads.</summary>
        private static int ReadAtMost(Stream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, total, count - total);
                if (read <= 0)
                {
                    break;
                }

                total += read;
            }

            return total;
        }

        /// <summary>
        /// True when <paramref name="path"/> is a symbolic link (to a file or a directory), without
        /// following it. Links must never be signed: writing through one would rewrite a file
        /// outside the tree being signed.
        /// </summary>
        internal static bool IsSymbolicLink(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception)
            {
                // A path that cannot be stat'ed (e.g. a dangling link on Windows) is not reported
                // as a link, but callers still fail closed on the read/write error.
                return false;
            }
        }

        /// <summary>
        /// Enumerates the regular files under <paramref name="directory"/>, recursively. Symbolic
        /// links — both file links and directory links — are never followed: each one is reported
        /// through <paramref name="onSymbolicLink"/> and skipped, so signing cannot escape the tree
        /// it was pointed at (and link cycles cannot recurse).
        /// </summary>
        internal static IEnumerable<string> EnumerateFilesWithoutLinks(string directory, Action<string>? onSymbolicLink)
        {
            var files = new List<string>();
            EnumerateFilesWithoutLinks(directory, files, onSymbolicLink);
            return files;
        }

        private static void EnumerateFilesWithoutLinks(string directory, List<string> files, Action<string>? onSymbolicLink)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (FileNotFoundException)
                {
                    onSymbolicLink?.Invoke(entry);
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    onSymbolicLink?.Invoke(entry);
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    onSymbolicLink?.Invoke(entry);
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    EnumerateFilesWithoutLinks(entry, files, onSymbolicLink);
                }
                else
                {
                    files.Add(entry);
                }
            }
        }

        private static CodesignState ClassifyCodesign(byte[] elf, out int csOff, out int csLen, out bool reusable, out string? anomaly) =>
            ClassifyCodesign(ElfImage.FromArray(elf), out csOff, out csLen, out reusable, out anomaly);

        /// <summary>
        /// Decides how to treat an existing .codesign section. <paramref name="reusable"/> is true
        /// when the block has exactly the geometry this signer writes (a 4KB page-aligned block
        /// that overlaps no other section or loadable segment), so it can be rewritten in place
        /// without touching any other byte. The image may be file-backed: nothing beyond the
        /// sections and the signature pages is read.
        /// </summary>
        private static CodesignState ClassifyCodesign(ElfImage elf, out int csOff, out int csLen, out bool reusable, out string? anomaly)
        {
            csOff = 0;
            csLen = 0;
            reusable = false;
            anomaly = null;

            try
            {
                (ulong eShOff, ushort eShnum, ushort eShstrndx) = ParseElfHeader(elf);
                if (!TryGetSectionNameTable(elf, eShOff, eShnum, eShstrndx, out int shstrOff, out int shstrSz))
                {
                    return Malformed("the section name table is out of bounds", out anomaly);
                }

                long csEntry = -1;
                int csIdx = -1;
                int count = 0;
                for (int i = 0; i < eShnum; i++)
                {
                    long entry = (long)eShOff + (long)i * 64;
                    uint nameOff = ReadU32(elf, (int)entry);
                    if ((ulong)nameOff + (ulong)s_codesignName.Length <= (ulong)shstrSz &&
                        ByteArrayEquals(elf, shstrOff + (int)nameOff, s_codesignName))
                    {
                        count++;
                        if (csEntry < 0)
                        {
                            csEntry = entry;
                            csIdx = i;
                        }
                    }
                }

                if (count == 0)
                {
                    return CodesignState.None;
                }

                if (count > 1)
                {
                    return Malformed($"{count} .codesign sections make the layout ambiguous", out anomaly);
                }

                ulong off = ReadU64(elf, (int)csEntry + 24);
                ulong size = ReadU64(elf, (int)csEntry + 32);
                if (off > (ulong)elf.Length || size > (ulong)elf.Length - off || size > int.MaxValue)
                {
                    return Malformed("the .codesign section is out of bounds", out anomaly);
                }

                csOff = (int)off;
                csLen = (int)size;

                if (OverlapsOtherContent(elf, eShOff, eShnum, csIdx, csOff, csLen, out string? overlap))
                {
                    return Malformed($"the .codesign section overlaps {overlap}", out anomaly);
                }

                if (csLen < PayloadSize)
                {
                    return Malformed($"the .codesign section is too small ({csLen} bytes)", out anomaly);
                }

                reusable = csLen == PageSize && (csOff % PageSize) == 0;
                if (!IsSelfSignDescriptor(elf, csOff, csLen))
                {
                    return CodesignState.Foreign;
                }

                return IsValidlySigned(elf, csOff, csLen) ? CodesignState.ValidSelfSign : CodesignState.StaleSelfSign;
            }
            catch (Exception ex)
            {
                return Malformed(ex.Message, out anomaly);
            }
        }

        private static CodesignState Malformed(string reason, out string? anomaly)
        {
            anomaly = reason;
            return CodesignState.Malformed;
        }

        private static bool OverlapsOtherContent(byte[] elf, ulong eShOff, ushort eShnum, int csIdx, int csOff, int csLen, out string? overlap) =>
            OverlapsOtherContent(ElfImage.FromArray(elf), eShOff, eShnum, csIdx, csOff, csLen, out overlap);

        /// <summary>
        /// True when the candidate .codesign block overlaps content owned by another section, a
        /// loadable segment or the ELF bookkeeping tables. Rewriting such a block would corrupt
        /// the binary, so the layout is refused instead.
        /// </summary>
        private static bool OverlapsOtherContent(ElfImage elf, ulong eShOff, ushort eShnum, int csIdx, int csOff, int csLen, out string? overlap)
        {
            overlap = null;
            ulong csStart = (ulong)csOff;
            ulong csEnd = (ulong)csOff + (ulong)csLen;

            // ELF header (always mapped).
            if (csStart < 64)
            {
                overlap = "the ELF header";
                return true;
            }

            // Program header table and loadable segments.
            ulong phOff = ReadU64(elf, 0x20);
            ushort phEnt = ReadU16(elf, 0x36);
            ushort phNum = ReadU16(elf, 0x38);
            if (phNum > 0 && phEnt > 0)
            {
                ulong phEnd = phOff + (ulong)phNum * phEnt;
                if (phEnd > (ulong)elf.Length || phEnd < phOff)
                {
                    overlap = "an out-of-bounds program header table";
                    return true;
                }

                if (csStart < phEnd && csEnd > phOff)
                {
                    overlap = "the program header table";
                    return true;
                }

                for (int i = 0; i < phNum; i++)
                {
                    ulong entry = phOff + (ulong)i * phEnt;
                    ulong pOff = ReadU64(elf, (int)entry + 8);
                    ulong pSize = ReadU64(elf, (int)entry + 32);
                    if (pSize == 0)
                    {
                        continue;
                    }

                    ulong pEnd = pOff + pSize;
                    if (pEnd < pOff || (csStart < pEnd && csEnd > pOff))
                    {
                        overlap = $"loadable segment #{i}";
                        return true;
                    }
                }
            }

            // Section header table.
            ulong shtEnd = eShOff + (ulong)eShnum * 64;
            if (csStart < shtEnd && csEnd > eShOff)
            {
                overlap = "the section header table";
                return true;
            }

            // Every other section that occupies file bytes (SHT_NOBITS has none).
            for (int i = 0; i < eShnum; i++)
            {
                if (i == csIdx)
                {
                    continue;
                }

                ulong entry = eShOff + (ulong)i * 64;
                if (ReadU32(elf, (int)entry + 4) == 8)
                {
                    continue;
                }

                ulong off = ReadU64(elf, (int)entry + 24);
                ulong size = ReadU64(elf, (int)entry + 32);
                if (size == 0)
                {
                    continue;
                }

                ulong end = off + size;
                if (end < off || (csStart < end && csEnd > off))
                {
                    overlap = $"section #{i}";
                    return true;
                }
            }

            return false;
        }

        private static bool IsSelfSignDescriptor(ElfImage elf, int csOff, int csLen)
        {
            if (csLen < PayloadSize)
            {
                return false;
            }

            uint type = ReadU32(elf, csOff);
            uint length = ReadU32(elf, csOff + 4);
            int dOff = csOff + 8;
            if (type != FsVerityDescriptorType || length != DescSize + HashOut)
            {
                return false;
            }

            if (elf[dOff] != 1 || elf[dOff + 1] != 1 || elf[dOff + 2] != 12 || elf[dOff + 255] != 3)
            {
                return false;
            }

            uint flags = ReadU32(elf, dOff + 112);
            return (flags & FlagSelfSign) != 0;
        }

        /// <summary>
        /// True when the file carries a .codesign payload that matches the current content: the
        /// descriptor is well-formed, the stored page Merkle root equals the recomputed one, and
        /// the stored signature equals SHA-256 of the descriptor with signSize zeroed.
        /// </summary>
        private static bool IsValidlySigned(ElfImage elf, int csOff, int csLen)
        {
            try
            {
                if (!IsSelfSignDescriptor(elf, csOff, csLen))
                {
                    return false;
                }

                int dOff = csOff + 8;
                uint signSize = ReadU32(elf, dOff + 4);
                ulong fileSize = ReadU64(elf, dOff + 8);
                uint flags = ReadU32(elf, dOff + 112);
                if (signSize != HashOut || fileSize != (ulong)elf.Length)
                {
                    return false;
                }

                byte[] root = elf.ReadRange(dOff + 16, HashOut);
                byte[] recomputedRoot = MerkleRootHash(elf, csOff, csLen);
                if (!BytesEqual(root, recomputedRoot))
                {
                    return false;
                }

                byte[] signature = elf.ReadRange(dOff + DescSize, HashOut);
                byte[] expectedSignature = Sha256(BuildDescriptor(0, fileSize, recomputedRoot, flags));
                return BytesEqual(signature, expectedSignature);
            }
            catch (Exception)
            {
                // A malformed ELF/section table means "not validly signed" for our purposes.
                return false;
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsElf64(byte[] data) =>
            data.Length >= 64 &&
            data[0] == 0x7f && data[1] == (byte)'E' && data[2] == (byte)'L' && data[3] == (byte)'F' &&
            data[4] == 2 && // ELFCLASS64
            data[5] == 1;   // ELFDATA2LSB: the signer reads/writes little-endian fields

        private static bool IsElf64(ElfImage data) =>
            data.Length >= 64 &&
            data[0] == 0x7f && data[1] == (byte)'E' && data[2] == (byte)'L' && data[3] == (byte)'F' &&
            data[4] == 2 && // ELFCLASS64
            data[5] == 1;   // ELFDATA2LSB: the signer reads/writes little-endian fields

        private static ushort ReadU16(ElfImage b, int off) => b.ReadU16(off);

        private static uint ReadU32(ElfImage b, int off) => b.ReadU32(off);

        private static ulong ReadU64(ElfImage b, int off) => b.ReadU64(off);

        private static bool ByteArrayEquals(ElfImage b, int off, byte[] name) => b.RangeEquals(off, name);

        /// <summary>
        /// Read-only view of an ELF64 image: either a byte array already in memory (the sign
        /// path) or an open FileStream (the probe/validation path). File-backed reads are
        /// bounded by the caller's access pattern: <see cref="SignFileInPlace"/> reads only the
        /// 64-byte header before deciding whether a file is an ELF, and
        /// <see cref="MerkleRootHash(ElfImage, int, int)"/> streams one 4KB page at a time.
        /// The stream is owned by the caller and is not disposed here.
        /// </summary>
        private sealed class ElfImage
        {
            private readonly byte[]? _data;
            private readonly FileStream? _stream;
            private readonly byte[] _scratch = new byte[8];

            public int Length { get; }

            private ElfImage(byte[]? data, FileStream? stream, int length)
            {
                _data = data;
                _stream = stream;
                Length = length;
            }

            public static ElfImage FromArray(byte[] data) => new ElfImage(data, null, data.Length);

            public static ElfImage FromStream(FileStream stream)
            {
                long length = stream.Length;
                if (length > int.MaxValue)
                {
                    // The signer (and the in-memory path it falls back to) addresses files
                    // with int offsets; refusing up front is fail-closed and readable.
                    throw new InvalidDataException("ELF too large to sign");
                }

                return new ElfImage(null, stream, (int)length);
            }

            public byte ReadByte(int offset)
            {
                if (_data != null)
                {
                    return _data[offset];
                }

                ReadExact(offset, _scratch, 0, 1);
                return _scratch[0];
            }

            /// <summary>Byte accessor used by the descriptor checks.</summary>
            public byte this[int offset] => ReadByte(offset);

            public ushort ReadU16(int offset)
            {
                if (_data != null)
                {
                    return (ushort)(_data[offset] | (_data[offset + 1] << 8));
                }

                ReadExact(offset, _scratch, 0, 2);
                return (ushort)(_scratch[0] | (_scratch[1] << 8));
            }

            public uint ReadU32(int offset)
            {
                if (_data != null)
                {
                    return (uint)(_data[offset] | (_data[offset + 1] << 8) | (_data[offset + 2] << 16) | (_data[offset + 3] << 24));
                }

                ReadExact(offset, _scratch, 0, 4);
                return (uint)(_scratch[0] | (_scratch[1] << 8) | (_scratch[2] << 16) | (_scratch[3] << 24));
            }

            public ulong ReadU64(int offset)
            {
                if (_data != null)
                {
                    return (ulong)_data[offset] | ((ulong)_data[offset + 1] << 8) | ((ulong)_data[offset + 2] << 16) | ((ulong)_data[offset + 3] << 24) |
                        ((ulong)_data[offset + 4] << 32) | ((ulong)_data[offset + 5] << 40) | ((ulong)_data[offset + 6] << 48) | ((ulong)_data[offset + 7] << 56);
                }

                ReadExact(offset, _scratch, 0, 8);
                return (ulong)_scratch[0] | ((ulong)_scratch[1] << 8) | ((ulong)_scratch[2] << 16) | ((ulong)_scratch[3] << 24) |
                    ((ulong)_scratch[4] << 32) | ((ulong)_scratch[5] << 40) | ((ulong)_scratch[6] << 48) | ((ulong)_scratch[7] << 56);
            }

            public byte[] ReadRange(int offset, int count)
            {
                byte[] result = new byte[count];
                ReadRangeInto(offset, result, count);
                return result;
            }

            public void ReadRangeInto(int offset, byte[] buffer, int count)
            {
                if (_data != null)
                {
                    Buffer.BlockCopy(_data, offset, buffer, 0, count);
                    return;
                }

                ReadExact(offset, buffer, 0, count);
            }

            public bool RangeEquals(int offset, byte[] expected)
            {
                if (offset < 0 || offset > Length - expected.Length)
                {
                    // Matches the array path's out-of-range behavior closely enough for the
                    // classification code, which treats every read failure as "not valid".
                    throw new InvalidDataException("read outside the ELF image");
                }

                if (_data != null)
                {
                    for (int i = 0; i < expected.Length; i++)
                    {
                        if (_data[offset + i] != expected[i])
                        {
                            return false;
                        }
                    }

                    return true;
                }

                byte[] probe = new byte[expected.Length];
                ReadExact(offset, probe, 0, expected.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    if (probe[i] != expected[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            private void ReadExact(int offset, byte[] buffer, int index, int count)
            {
                if (offset < 0 || count < 0 || offset > Length - count)
                {
                    throw new InvalidDataException("read outside the ELF image");
                }

                // ReadExact is only reached for a file-backed image (array reads never get here).
                FileStream stream = _stream!;
                stream.Seek(offset, SeekOrigin.Begin);
                int total = 0;
                while (total < count)
                {
                    int read = stream.Read(buffer, index + total, count - total);
                    if (read <= 0)
                    {
                        throw new InvalidDataException("unexpected end of file while reading the ELF image");
                    }

                    total += read;
                }
            }
        }

        private static ushort ReadU16(byte[] b, int off) => (ushort)(b[off] | (b[off + 1] << 8));

        private static uint ReadU32(byte[] b, int off) =>
            (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));

        private static ulong ReadU64(byte[] b, int off) =>
            (ulong)b[off] | ((ulong)b[off + 1] << 8) | ((ulong)b[off + 2] << 16) | ((ulong)b[off + 3] << 24) |
            ((ulong)b[off + 4] << 32) | ((ulong)b[off + 5] << 40) | ((ulong)b[off + 6] << 48) | ((ulong)b[off + 7] << 56);

        private static void WriteU16(byte[] b, int off, ushort v)
        {
            b[off] = (byte)v;
            b[off + 1] = (byte)(v >> 8);
        }

        private static void WriteU32(byte[] b, int off, uint v)
        {
            b[off] = (byte)v;
            b[off + 1] = (byte)(v >> 8);
            b[off + 2] = (byte)(v >> 16);
            b[off + 3] = (byte)(v >> 24);
        }

        private static void WriteU64(byte[] b, int off, ulong v)
        {
            b[off] = (byte)v;
            b[off + 1] = (byte)(v >> 8);
            b[off + 2] = (byte)(v >> 16);
            b[off + 3] = (byte)(v >> 24);
            b[off + 4] = (byte)(v >> 32);
            b[off + 5] = (byte)(v >> 40);
            b[off + 6] = (byte)(v >> 48);
            b[off + 7] = (byte)(v >> 56);
        }

        private static ulong AlignUp(ulong v, ulong a) => (v + a - 1) / a * a;

        // NOTE: SHA256.HashData is not available on net472, which this multi-targeted task
        // project also builds for (Microsoft.NET.Build.Tasks.csproj -> TargetFrameworks).
        private static byte[] Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(data);
            }
        }

        private static (ulong eShOff, ushort eShnum, ushort eShstrndx) ParseElfHeader(byte[] elf) =>
            ParseElfHeader(ElfImage.FromArray(elf));

        private static (ulong eShOff, ushort eShnum, ushort eShstrndx) ParseElfHeader(ElfImage elf)
        {
            if (!IsElf64(elf))
            {
                throw new InvalidDataException("not ELF64");
            }

            ulong eShOff = ReadU64(elf, EShOff);
            ushort eShentsize = ReadU16(elf, EShentsize);
            ushort eShnum = ReadU16(elf, EShnum);
            ushort eShstrndx = ReadU16(elf, EShstrndx);
            if (eShentsize != 64 || eShOff == 0 || eShnum == 0 || eShstrndx >= eShnum)
            {
                throw new InvalidDataException("ELF has no usable section header table");
            }

            if (eShOff > (ulong)elf.Length || (ulong)eShnum > ((ulong)elf.Length - eShOff) / 64)
            {
                throw new InvalidDataException("section header table out of bounds");
            }

            return (eShOff, eShnum, eShstrndx);
        }

        private static bool TryGetSectionNameTable(byte[] elf, ulong eShOff, ushort eShnum, ushort eShstrndx, out int shstrOff, out int shstrSz) =>
            TryGetSectionNameTable(ElfImage.FromArray(elf), eShOff, eShnum, eShstrndx, out shstrOff, out shstrSz);

        private static bool TryGetSectionNameTable(ElfImage elf, ulong eShOff, ushort eShnum, ushort eShstrndx, out int shstrOff, out int shstrSz)
        {
            shstrOff = 0;
            shstrSz = 0;
            if (eShstrndx >= eShnum)
            {
                return false;
            }

            ulong entry = eShOff + (ulong)eShstrndx * 64;
            ulong off = ReadU64(elf, (int)entry + 24);
            ulong size = ReadU64(elf, (int)entry + 32);
            if (off > (ulong)elf.Length || size > (ulong)elf.Length - off || size > int.MaxValue)
            {
                return false;
            }

            shstrOff = (int)off;
            shstrSz = (int)size;
            return true;
        }

        private static long FindSectionByName(byte[] elf, ulong eShOff, ushort eShnum, ushort eShstrndx, byte[] name) =>
            FindSectionByName(ElfImage.FromArray(elf), eShOff, eShnum, eShstrndx, name);

        private static long FindSectionByName(ElfImage elf, ulong eShOff, ushort eShnum, ushort eShstrndx, byte[] name)
        {
            if (!TryGetSectionNameTable(elf, eShOff, eShnum, eShstrndx, out int shstrOff, out int shstrSz))
            {
                return -1;
            }

            for (int i = 0; i < eShnum; i++)
            {
                long entry = (long)eShOff + (long)i * 64;
                uint nameOff = ReadU32(elf, (int)entry);
                if ((ulong)nameOff + (ulong)name.Length <= (ulong)shstrSz &&
                    ByteArrayEquals(elf, shstrOff + (int)nameOff, name))
                {
                    return entry;
                }
            }

            return -1;
        }

        private static bool ByteArrayEquals(byte[] b, int off, byte[] name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                if (b[off + i] != name[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static ushort NewShstrndx(ushort oldShstrndx, int csIdx) =>
            csIdx < oldShstrndx ? (ushort)(oldShstrndx - 1) : oldShstrndx;

        /// <summary>
        /// Removes the .codesign section entry from the section header table and appends the
        /// rebuilt name table/section header table after the original bytes. The original bytes
        /// are kept in place (including anything that followed the signature block, such as a
        /// SingleFile bundle) except for the old signature payload, which is zeroed only when it
        /// cannot overlap any other section or loadable segment.
        /// </summary>
        internal static byte[] StripCodesign(byte[] buf, out bool removed)
        {
            removed = false;
            (ulong eShOff, ushort eShnum, ushort eShstrndx) = ParseElfHeader(buf);

            long csEntryOff = FindSectionByName(buf, eShOff, eShnum, eShstrndx, s_codesignName);
            if (csEntryOff < 0)
            {
                return buf;
            }

            if (eShnum < 2)
            {
                throw new InvalidDataException("refusing to strip the only section in the file");
            }

            int csIdx = (int)((ulong)csEntryOff - eShOff) / 64;
            if (!TryGetSectionNameTable(buf, eShOff, eShnum, eShstrndx, out int shstrStart, out int shstrTotal))
            {
                throw new InvalidDataException("shstrtab out of bounds");
            }

            int count = 0;
            for (int i = 0; i < eShnum; i++)
            {
                long entry = (long)eShOff + (long)i * 64;
                uint nameOff = ReadU32(buf, (int)entry);
                if ((ulong)nameOff + (ulong)s_codesignName.Length <= (ulong)shstrTotal &&
                    ByteArrayEquals(buf, shstrStart + (int)nameOff, s_codesignName))
                {
                    count++;
                }
            }

            if (count != 1)
            {
                throw new InvalidDataException($"refusing to strip: {count} .codesign sections make the layout ambiguous");
            }

            if (csIdx == eShstrndx)
            {
                throw new InvalidDataException("refusing to strip: the .codesign section is the section name table");
            }

            if (csIdx == 0)
            {
                throw new InvalidDataException("refusing to strip: the .codesign section is the reserved section #0");
            }

            uint csNameOff = ReadU32(buf, (int)csEntryOff);
            int csNameLen = s_codesignName.Length;
            if ((ulong)shstrTotal < (ulong)csNameLen || (ulong)csNameOff + (ulong)csNameLen > (ulong)shstrTotal)
            {
                throw new InvalidDataException("the .codesign name lies outside the section name table");
            }

            ulong csSecOff = ReadU64(buf, (int)csEntryOff + 24);
            ulong csSecSize = ReadU64(buf, (int)csEntryOff + 32);
            if (csSecOff > (ulong)buf.Length || csSecSize > (ulong)buf.Length - csSecOff || csSecSize > int.MaxValue)
            {
                throw new InvalidDataException("the .codesign section is out of bounds");
            }

            int newShstrSz = shstrTotal - csNameLen;
            byte[] newShstr = new byte[newShstrSz];
            if (csNameOff > 0)
            {
                Buffer.BlockCopy(buf, shstrStart, newShstr, 0, (int)csNameOff);
            }

            Buffer.BlockCopy(buf, shstrStart + (int)csNameOff + csNameLen, newShstr, (int)csNameOff, newShstrSz - (int)csNameOff);

            int newShnum = eShnum - 1;
            byte[] newSht = new byte[newShnum * 64];
            int dst = 0;
            for (int i = 0; i < eShnum; i++)
            {
                if (i == csIdx)
                {
                    continue;
                }

                Buffer.BlockCopy(buf, (int)eShOff + i * 64, newSht, dst, 64);
                dst += 64;
            }

            // Append the rebuilt tables after the original end of file: no byte of the input is
            // ever removed or shifted, so trailing data (SingleFile bundle, appended blobs) and
            // any absolute offset it contains stay valid.
            int newShstrOff = buf.Length;
            int newShtOff = (int)AlignUp((ulong)(newShstrOff + newShstrSz), 8);
            int newTotal = newShtOff + newShnum * 64;

            byte[] outBuf = new byte[newTotal];
            Buffer.BlockCopy(buf, 0, outBuf, 0, buf.Length);
            Buffer.BlockCopy(newShstr, 0, outBuf, newShstrOff, newShstrSz);
            Buffer.BlockCopy(newSht, 0, outBuf, newShtOff, newShnum * 64);

            int shstrEntryOffInNew = NewShstrndx(eShstrndx, csIdx) * 64;
            WriteU64(outBuf, newShtOff + shstrEntryOffInNew + 24, (ulong)newShstrOff);
            WriteU64(outBuf, newShtOff + shstrEntryOffInNew + 32, (ulong)newShstrSz);

            for (int i = 0; i < newShnum; i++)
            {
                int e = newShtOff + i * 64;
                uint noff = ReadU32(outBuf, e);
                if (noff > csNameOff)
                {
                    WriteU32(outBuf, e, noff - (uint)csNameLen);
                }
            }

            WriteU64(outBuf, EShOff, (ulong)newShtOff);
            WriteU16(outBuf, EShnum, (ushort)newShnum);
            if (csIdx < eShstrndx)
            {
                WriteU16(outBuf, EShstrndx, (ushort)(eShstrndx - 1));
            }

            // The old signature payload is only erased when it is a self-contained block that no
            // other section or segment can reach; otherwise its bytes are body data and stay.
            bool overlaps = OverlapsOtherContent(outBuf, eShOff, eShnum, csIdx, (int)csSecOff, (int)csSecSize, out _);
            int zeroed = overlaps ? 0 : (int)csSecSize;
            if (zeroed > 0)
            {
                Array.Clear(outBuf, (int)csSecOff, zeroed);
            }

            ValidateNoDataLoss(outBuf, buf, (int)csSecOff, zeroed);
            removed = true;
            return outBuf;
        }

        private static (byte[] buf, int csOff) InjectCodesignSection(byte[] elf)
        {
            (ulong eShOff, ushort eShnum, ushort eShstrndx) = ParseElfHeader(elf);

            if (!TryGetSectionNameTable(elf, eShOff, eShnum, eShstrndx, out int shstrStart, out int shstrSz))
            {
                throw new InvalidDataException("shstrtab out of bounds");
            }

            ulong curEnd = eShOff + (ulong)eShnum * 64;
            for (int i = 0; i < eShnum; i++)
            {
                ulong e = eShOff + (ulong)i * 64;
                uint shType = ReadU32(elf, (int)(e + 4));
                ulong off = ReadU64(elf, (int)(e + 24));
                ulong sz = shType == 8 ? 0 : ReadU64(elf, (int)(e + 32));
                if (off + sz > curEnd)
                {
                    curEnd = off + sz;
                }
            }

            // SingleFile bundles (and other data) may follow the section header table but are not
            // covered by any section. Preserve everything through the real end of file so the
            // bundle survives signing (otherwise the apphost is truncated to its ELF-only part).
            if ((ulong)elf.Length > curEnd)
            {
                curEnd = (ulong)elf.Length;
            }

            ulong csOffAligned = AlignUp(curEnd, PageSize);
            if (csOffAligned > int.MaxValue)
            {
                throw new InvalidDataException("ELF too large to sign");
            }

            int csOff = (int)csOffAligned;

            byte[] newShstr = new byte[shstrSz + s_codesignName.Length];
            Buffer.BlockCopy(elf, shstrStart, newShstr, 0, shstrSz);
            Buffer.BlockCopy(s_codesignName, 0, newShstr, shstrSz, s_codesignName.Length);
            int newShstrSz = newShstr.Length;
            uint csShname = (uint)shstrSz;

            int newShstrOff = csOff + PageSize;
            int newShtOff = (int)AlignUp((ulong)(newShstrOff + newShstrSz), 8);
            int newShnum = eShnum + 1;
            int newTotal = newShtOff + newShnum * 64;

            byte[] buf = new byte[newTotal];
            int copyLen = Math.Min(elf.Length, Math.Min(newTotal, csOff));
            Buffer.BlockCopy(elf, 0, buf, 0, copyLen);

            Buffer.BlockCopy(newShstr, 0, buf, newShstrOff, newShstrSz);
            Buffer.BlockCopy(elf, (int)eShOff, buf, newShtOff, (int)eShnum * 64);

            int csE = newShtOff + (int)eShnum * 64;
            WriteU32(buf, csE, csShname); // sh_name
            WriteU32(buf, csE + 4, 1); // sh_type = SHT_PROGBITS
            WriteU64(buf, csE + 24, (ulong)csOff); // sh_offset
            WriteU64(buf, csE + 32, PageSize); // sh_size
            WriteU64(buf, csE + 48, PageSize); // sh_addralign

            int shstrENew = newShtOff + (int)eShstrndx * 64;
            WriteU64(buf, shstrENew + 24, (ulong)newShstrOff);
            WriteU64(buf, shstrENew + 32, (ulong)newShstrSz);

            WriteU64(buf, EShOff, (ulong)newShtOff);
            WriteU16(buf, EShnum, (ushort)newShnum);

            return (buf, csOff);
        }

        /// <summary>
        /// Writes the fs-verity descriptor and signature into an already zeroed
        /// [<paramref name="csOff"/>, <paramref name="csOff"/> + <paramref name="csLen"/>) block.
        /// </summary>
        private static void WriteSignature(byte[] buf, int csOff, int csLen)
        {
            if (csOff < 0 || csLen < PayloadSize || (long)csOff + csLen > buf.Length)
            {
                throw new InvalidDataException("signature payload exceeds file length");
            }

            ulong fileSize = (ulong)buf.Length;
            byte[] root = MerkleRootHash(buf, csOff, csLen);
            byte[] descForDigest = BuildDescriptor(0, fileSize, root, FlagSelfSign);
            byte[] signature = Sha256(descForDigest);
            byte[] descOnDisk = BuildDescriptor(HashOut, fileSize, root, FlagSelfSign);

            WriteU32(buf, csOff, FsVerityDescriptorType);
            WriteU32(buf, csOff + 4, (uint)(DescSize + HashOut));
            Buffer.BlockCopy(descOnDisk, 0, buf, csOff + 8, DescSize);
            Buffer.BlockCopy(signature, 0, buf, csOff + 8 + DescSize, HashOut);
        }

        private static byte[] MerkleRootHash(byte[] data, int csOff, int csLen) =>
            MerkleRootHash(ElfImage.FromArray(data), csOff, csLen);

        /// <summary>
        /// fs-verity page Merkle root. Pages are read one at a time from the image, so a
        /// file-backed image (the already-signed validation path) never holds more than one
        /// 4KB page plus the leaf-hash table (32 bytes per page) in memory.
        /// </summary>
        private static byte[] MerkleRootHash(ElfImage data, int csOff, int csLen)
        {
            if (data.Length == 0)
            {
                return Sha256(new byte[PageSize]);
            }

            int npages = (data.Length + PageSize - 1) / PageSize;
            int csPageBegin = csOff / PageSize;
            int csPageEnd = (csOff + csLen + PageSize - 1) / PageSize;

            byte[] hashes = new byte[npages * HashOut];
            byte[] page = new byte[PageSize];
            for (int i = 0; i < npages; i++)
            {
                if (csLen > 0 && i >= csPageBegin && i < csPageEnd)
                {
                    continue; // codesign pages: zero leaf hash
                }

                int off = i * PageSize;
                int n = Math.Min(PageSize, data.Length - off);
                Array.Clear(page, 0, PageSize);
                data.ReadRangeInto(off, page, n);
                byte[] h = Sha256(page);
                Buffer.BlockCopy(h, 0, hashes, i * HashOut, HashOut);
            }

            if (npages == 1)
            {
                byte[] root = new byte[HashOut];
                Buffer.BlockCopy(hashes, 0, root, 0, HashOut);
                return root;
            }

            byte[] cur = hashes;
            while (true)
            {
                int packed = cur.Length;
                if (packed <= PageSize)
                {
                    byte[] combinePage = new byte[PageSize];
                    Buffer.BlockCopy(cur, 0, combinePage, 0, packed);
                    return Sha256(combinePage);
                }

                int nextPages = (packed + PageSize - 1) / PageSize;
                byte[] next = new byte[nextPages * HashOut];
                for (int i = 0; i < nextPages; i++)
                {
                    byte[] combinePage = new byte[PageSize];
                    int off = i * PageSize;
                    int n = Math.Min(PageSize, packed - off);
                    Buffer.BlockCopy(cur, off, combinePage, 0, n);
                    byte[] h = Sha256(combinePage);
                    Buffer.BlockCopy(h, 0, next, i * HashOut, HashOut);
                }

                cur = next;
            }
        }

        private static byte[] BuildDescriptor(uint signSize, ulong fileSize, byte[] root, uint flags)
        {
            byte[] d = new byte[DescSize];
            d[0] = 1; // version
            d[1] = 1; // hashAlgorithm = SHA-256
            d[2] = 12; // log2BlockSize = 2^12 = 4096
            d[3] = 0; // saltSize
            WriteU32(d, 4, signSize);
            WriteU64(d, 8, fileSize);
            Buffer.BlockCopy(root, 0, d, 16, HashOut); // rootHash left-aligned
            WriteU32(d, 112, flags);
            d[255] = 3; // csVersion
            return d;
        }

        internal static byte[] SignElf(byte[] elf, bool force)
        {
            if (!IsElf64(elf))
            {
                throw new InvalidDataException("not ELF64");
            }

            CodesignState state = ClassifyCodesign(elf, out int csOff, out int csLen, out bool reusable, out string? anomaly);
            if (state == CodesignState.Malformed)
            {
                throw new InvalidDataException($"refusing to sign: {anomaly}");
            }

            if (state == CodesignState.None)
            {
                return InjectAndSign(elf);
            }

            if (!force)
            {
                throw new InvalidDataException("already has a .codesign section; strip first or use --force");
            }

            if (reusable)
            {
                // The existing block has exactly the geometry this signer produces: rewrite the
                // signature in place. Nothing else in the file moves, so a SingleFile bundle (or
                // any other trailing data) is preserved and repeated --force re-signs do not grow
                // the file.
                byte[] resigned = (byte[])elf.Clone();
                Array.Clear(resigned, csOff, csLen);
                WriteSignature(resigned, csOff, csLen);
                ValidateNoDataLoss(resigned, elf, csOff, csLen);
                ValidateSigned(resigned, csOff);
                return resigned;
            }

            // Foreign geometry: drop the section entry but keep every original byte, then append a
            // fresh self-contained block after the end of file.
            return InjectAndSign(StripCodesign(elf, out _));
        }

        private static byte[] InjectAndSign(byte[] elf)
        {
            (byte[] buf, int csOff) = InjectCodesignSection(elf);
            WriteSignature(buf, csOff, PageSize);
            // Nothing that was in the input may be dropped or rewritten: the .codesign block is
            // appended after the original bytes.
            ValidateNoDataLoss(buf, elf, changedStart: 0, changedLength: 0);
            ValidateSigned(buf, csOff);
            return buf;
        }

        /// <summary>
        /// Asserts that signing did not drop or rewrite any input byte: every byte outside
        /// [<paramref name="changedStart"/>, <paramref name="changedStart"/> +
        /// <paramref name="changedLength"/>) must be present unchanged at the same offset, and
        /// the output may only be longer than the input. Only the section-table bookkeeping in
        /// the ELF header (e_shoff/e_shnum/e_shstrndx) may change, because the rebuilt section
        /// header table is relocated.
        /// </summary>
        private static void ValidateNoDataLoss(byte[] output, byte[] input, int changedStart, int changedLength)
        {
            if (output.Length < input.Length)
            {
                throw new InvalidDataException($"signed output shrank from {input.Length} to {output.Length} bytes");
            }

            for (int i = 0; i < input.Length; i++)
            {
                if (changedLength > 0 && i >= changedStart && i < changedStart + changedLength)
                {
                    continue;
                }

                if (IsSectionTableBookkeeping(i))
                {
                    continue;
                }

                if (output[i] != input[i])
                {
                    throw new InvalidDataException($"signed output differs from the input at offset {i}: data must never be dropped or rewritten");
                }
            }
        }

        private static bool IsSectionTableBookkeeping(int offset) =>
            (offset >= EShOff && offset < EShOff + 8) ||
            (offset >= EShnum && offset < EShnum + 2) ||
            (offset >= EShstrndx && offset < EShstrndx + 2);

        // Mirrors Mach-O Validate: after building the signature, re-parse the result to ensure the
        // layout is self-consistent — the .codesign data is at csOff, nothing that must be covered
        // by the signature (the executable and any SingleFile bundle) was dropped, and the section
        // header table is still readable.
        private static void ValidateSigned(byte[] signed, int csOff)
        {
            if (csOff < 0 || signed.Length < csOff + PageSize)
            {
                throw new InvalidDataException("signature payload exceeds file length");
            }

            (ulong eShOff, ushort eShnum, ushort eShstrndx) = ParseElfHeader(signed);
            long csEntry = FindSectionByName(signed, eShOff, eShnum, eShstrndx, s_codesignName);
            if (csEntry < 0)
            {
                throw new InvalidDataException("signed output is missing the .codesign section");
            }

            ulong off = ReadU64(signed, (int)csEntry + 24);
            ulong size = ReadU64(signed, (int)csEntry + 32);
            if (off != (ulong)csOff || size != PageSize || size > (ulong)signed.Length - off)
            {
                throw new InvalidDataException("signed output has an inconsistent .codesign section");
            }
        }
    }
}

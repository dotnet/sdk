// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenAOpenHarmonyElfSigner
    {
        [TestMethod]
        public void ItSignsAnUnsignedElfAndSkipsAnAlreadyValidSignature()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllBytes(path, CreateMinimalElf64());
            try
            {
                ElfSigner.SignFileInPlace(path).Should().Be(ElfSigner.SignOutcome.Signed);
                ElfSigner.SignFileInPlace(path).Should().Be(ElfSigner.SignOutcome.AlreadyValid);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ItLeavesNonElfFilesUntouched()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.WriteAllText(path, "not an ELF");
            try
            {
                ElfSigner.SignFileInPlace(path).Should().Be(ElfSigner.SignOutcome.NotElf);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ItSkipsSymbolicLinks()
        {
            string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            try
            {
                string target = Path.Combine(directory, "target.bin");
                File.WriteAllBytes(target, CreateMinimalElf64());
                string link = Path.Combine(directory, "link.bin");
                File.CreateSymbolicLink(link, target);

                var reportedLinks = new List<string>();
                string[] files = ElfSigner.EnumerateFilesWithoutLinks(directory, reportedLinks.Add).ToArray();

                files.Should().Contain(target);
                files.Should().NotContain(link);
                reportedLinks.Should().Contain(link);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void ItRecognizesKnownNonExecutableExtensions()
        {
            ElfSigner.HasNonExecutableExtension("app.dll").Should().BeTrue();
            ElfSigner.HasNonExecutableExtension("app.pdb").Should().BeTrue();
            ElfSigner.HasNonExecutableExtension("apphost").Should().BeFalse();
        }

        private static byte[] CreateMinimalElf64()
        {
            const int sectionHeaderOffset = 64;
            const int sectionHeaderSize = 64;
            const ushort sectionCount = 2;
            const ushort sectionNameTableIndex = 1;

            byte[] sectionNames = Encoding.ASCII.GetBytes("\0.shstrtab\0");
            int sectionNamesOffset = sectionHeaderOffset + sectionCount * sectionHeaderSize;
            byte[] elf = new byte[sectionNamesOffset + sectionNames.Length];

            elf[0] = 0x7f;
            elf[1] = (byte)'E';
            elf[2] = (byte)'L';
            elf[3] = (byte)'F';
            elf[4] = 2; // ELFCLASS64
            elf[5] = 1; // ELFDATA2LSB
            elf[6] = 1; // EV_CURRENT
            WriteU16(elf, 16, 2); // ET_EXEC
            WriteU16(elf, 18, 183); // EM_AARCH64
            WriteU32(elf, 20, 1); // EV_CURRENT
            WriteU64(elf, 40, sectionHeaderOffset); // e_shoff
            WriteU16(elf, 52, 64); // e_ehsize
            WriteU16(elf, 58, sectionHeaderSize); // e_shentsize
            WriteU16(elf, 60, sectionCount); // e_shnum
            WriteU16(elf, 62, sectionNameTableIndex); // e_shstrndx

            int namesHeader = sectionHeaderOffset + sectionNameTableIndex * sectionHeaderSize;
            WriteU32(elf, namesHeader, 1); // sh_name: offset of ".shstrtab"
            WriteU32(elf, namesHeader + 4, 3); // SHT_STRTAB
            WriteU64(elf, namesHeader + 24, (ulong)sectionNamesOffset); // sh_offset
            WriteU64(elf, namesHeader + 32, (ulong)sectionNames.Length); // sh_size
            WriteU64(elf, namesHeader + 48, 1); // sh_addralign

            Buffer.BlockCopy(sectionNames, 0, elf, sectionNamesOffset, sectionNames.Length);
            return elf;
        }

        private static void WriteU16(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteU32(byte[] buffer, int offset, uint value)
        {
            for (int i = 0; i < 4; i++)
            {
                buffer[offset + i] = (byte)(value >> (8 * i));
            }
        }

        private static void WriteU64(byte[] buffer, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[offset + i] = (byte)(value >> (8 * i));
            }
        }
    }
}

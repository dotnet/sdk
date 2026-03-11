// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Buffers.Binary;
using System.Diagnostics;

namespace Microsoft.NET.TestFramework.Utilities
{
    public static class MachOSignature
    {
        /// <summary>
        /// Calls the 'codesign' utility to verify if the file has a valid Mach-O signature.
        /// </summary>
        /// <param name="file">The Mach Object file to check.</param>
        /// <param name="log">The output helper for logging.</param>
        /// <returns>True if the file has a valid Mach-O signature, otherwise false.</returns>
#if NET
        [SupportedOSPlatform("osx")]
#endif
        public static bool HasValidMachOSignature(FileInfo file, ITestOutputHelper log)
        {
            var codesignPath = @"/usr/bin/codesign";
            return new RunExeCommand(log, codesignPath, "-v", file.FullName)
                .Execute().ExitCode == 0;
        }

        /// <summary>
        ///  Reads the Mach-O load commands and returns true if an LC_CODE_SIGNATURE command is found, otherwise returns false. Does not validate the signature.
        /// </summary>
        /// <param name="file">The Mach Object file to check.</param>
        /// <returns>True if the file has a Mach-O signature load command, otherwise false.</returns>
        public static bool HasMachOSignatureLoadCommand(FileInfo file)
        {
            /* Mach-O files have the following structure:
             * 32 byte header beginning with a magic number and info about the file and load commands
             * A series of load commands with the following structure:
             * - 4-byte command type
             * - 4-byte command size
             * - variable length command-specific data
             */
            const uint LC_CODE_SIGNATURE = 0x0000001D;
            using (var stream = file.OpenRead())
            {
                // Read the MachO magic number to determine endianness
                Span<byte> eightByteBuffer = stackalloc byte[8];
                stream.ReadExactly(eightByteBuffer);
                // Determine if the magic number is in the same or opposite endianness as the runtime
                bool reverseEndinanness = BitConverter.ToUInt32(eightByteBuffer.Slice(0, 4)) switch
                {
                    0xFEEDFACF => false,
                    0xCFFAEDFE => true,
                    _ => throw new InvalidOperationException("Not a 64-bit Mach-O file")
                };
                // 4-byte value at offset 16 is the number of load commands
                // 4-byte value at offset 20 is the size of the load commands
                stream.Position = 16;
                ReadUInts(stream, eightByteBuffer, out uint loadCommandsCount, out uint loadCommandsSize);
                // Mach-0 64 byte headers are 32 bytes long, and the first load command will be right after
                stream.Position = 32;
                bool hasSignature = false;
                for (int commandIndex = 0; commandIndex < loadCommandsCount; commandIndex++)
                {
                    ReadUInts(stream, eightByteBuffer, out uint commandType, out uint commandSize);
                    if (commandType == LC_CODE_SIGNATURE)
                    {
                        hasSignature = true;
                    }
                    stream.Position += commandSize - eightByteBuffer.Length;
                }
                Debug.Assert(stream.Position == loadCommandsSize + 32);
                return hasSignature;

                void ReadUInts(Stream stream, Span<byte> buffer, out uint val1, out uint val2)
                {
                    stream.ReadExactly(buffer);
                    val1 = BitConverter.ToUInt32(buffer.Slice(0, 4));
                    val2 = BitConverter.ToUInt32(buffer.Slice(4, 4));
                    if (reverseEndinanness)
                    {
                        val1 = BinaryPrimitives.ReverseEndianness(val1);
                        val2 = BinaryPrimitives.ReverseEndianness(val2);
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    internal static class ComHost
    {
        private const int ClsidmapResourceId = 64;
        private const int ClsidmapResourceType = 1024;

        /// <summary>
        /// Create an ComHost with an embedded CLSIDMap file to map CLSIDs to .NET Classes.
        /// </summary>
        /// <param name="comHostSourceFilePath">The path of Apphost template, which has the place holder</param>
        /// <param name="comHostDestinationFilePath">The destination path for desired location to place, including the file name</param>
        /// <param name="intermediateAssembly">Path to the intermediate assembly, used for copying resources to PE apphosts.</param>
        /// <param name="clsidmap">The path to the *.clsidmap file.</param>
        /// <param name="log">Specify the logger used to log warnings and messages. If null, no logging is done.</param>
        public static void Create(
            string comHostSourceFilePath,
            string comHostDestinationFilePath,
            string clsidmapFilePath,
            bool embedClsidMap)
        {
            var destinationDirectory = new FileInfo(comHostDestinationFilePath).Directory.FullName;
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Copy apphost to destination path so it inherits the same attributes/permissions.
            File.Copy(comHostSourceFilePath, comHostDestinationFilePath, overwrite: true);

            if (ResourceUpdater.IsSupportedOS() && embedClsidMap)
            {
                string clsidMap = File.ReadAllText(clsidmapFilePath);
                byte[] clsidMapBytes = Encoding.UTF8.GetBytes(clsidMap);

                ResourceUpdater updater = new ResourceUpdater(comHostDestinationFilePath);
                updater.AddResource(clsidMapBytes, (IntPtr)ClsidmapResourceType, (IntPtr)ClsidmapResourceId);
                updater.Update();
            }
        }


        /// <summary>
        /// The first two bytes of a PE file are a constant signature.
        /// </summary>
        private const UInt16 PEFileSignature = 0x5A4D;

        /// <summary>
        /// The offset of the PE header pointer in the DOS header.
        /// </summary>
        private const int PEHeaderPointerOffset = 0x3C;

        /// <summary>
        /// Check whether the apphost file is a windows PE image by looking at the first few bytes.
        /// </summary>
        /// <param name="accessor">The memory accessor which has the apphost file opened.</param>
        /// <returns>true if the accessor represents a PE image, false otherwise.</returns>
        private static unsafe bool IsPEImage(MemoryMappedViewAccessor accessor)
        {
            byte* pointer = null;

            try
            {
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                byte* bytes = pointer + accessor.PointerOffset;

                // https://en.wikipedia.org/wiki/Portable_Executable
                // Validate that we're looking at Windows PE file
                if (((UInt16*)bytes)[0] != PEFileSignature || accessor.Capacity < PEHeaderPointerOffset + sizeof(UInt32))
                {
                    return false;
                }
                return true;
            }
            finally
            {
                if (pointer != null)
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    static partial class FileUtilities
    {
        public static Version GetFileVersion(string sourcePath)
        {
            var fvi = FileVersionInfo.GetVersionInfo(sourcePath);

            if (fvi != null)
            {
                return new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
            }

            return null;
        }

        static readonly HashSet<string> s_assemblyExtensions = new HashSet<string>(new[] { ".dll", ".exe", ".winmd" }, StringComparer.OrdinalIgnoreCase);
        public static Version TryGetAssemblyVersion(string sourcePath)
        {
            var extension = Path.GetExtension(sourcePath);

            return s_assemblyExtensions.Contains(extension) ? GetAssemblyVersion(sourcePath) : null;
        }

        private static Version GetAssemblyVersion(string sourcePath)
        {
            using (var assemblyStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                Version result = null;
                try
                {
                    using (PEReader peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen))
                    {
                        if (peReader.HasMetadata)
                        {
                            MetadataReader reader = peReader.GetMetadataReader();
                            if (reader.IsAssembly)
                            {
                                result = reader.GetAssemblyDefinition().Version;
                            }
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // not a PE
                }

                return result;
            }
        }
    }
}

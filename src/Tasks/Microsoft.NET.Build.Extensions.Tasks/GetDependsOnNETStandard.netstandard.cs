// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP1_0
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.NET.Build.Tasks
{
    public partial class GetDependsOnNETStandard
    {
        internal static void GetFileDependsOn(string filePath,
            out bool dependsOnNETStandard,
            out bool dependsOnNuGetCompression,
            out bool dependsOnNuGetHttp)
        {
            dependsOnNETStandard = false;
            dependsOnNuGetCompression = false;
            dependsOnNuGetHttp = false;
            using (var assemblyStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            using (PEReader peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen))
            {
                if (peReader.HasMetadata)
                {
                    MetadataReader reader = peReader.GetMetadataReader();
                    if (reader.IsAssembly)
                    {
                        foreach (var referenceHandle in reader.AssemblyReferences)
                        {
                            AssemblyReference reference = reader.GetAssemblyReference(referenceHandle);

                            if (reader.StringComparer.Equals(reference.Name, NetStandardAssemblyName))
                            {
                                dependsOnNETStandard = true;
                            }
                            
                            if (reader.StringComparer.Equals(reference.Name, SystemRuntimeAssemblyName) &&
                                reference.Version >= SystemRuntimeMinVersion)
                            {
                                dependsOnNETStandard = true;
                            }

                            if (reader.StringComparer.Equals(reference.Name, SystemIOCompressionAssemblyName) &&
                                reference.Version >= SystemIOCompressionMinVersion)
                            {
                                dependsOnNuGetCompression = true;
                            }

                            if (reader.StringComparer.Equals(reference.Name, SystemNetHttpAssemblyName) &&
                                reference.Version >= SystemNetHttpMinVersion)
                            {
                                dependsOnNuGetHttp = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
#endif

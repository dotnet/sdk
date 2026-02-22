// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.NET.HostModel.ComHost;

namespace Microsoft.NET.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public class GenerateClsidMap : TaskBase, IMultiThreadableTask
    {
        [Required]
        public string IntermediateAssembly { get; set; }

        [Required]
        public string ClsidMapDestinationPath { get; set; }

        public TaskEnvironment TaskEnvironment { get; set; }

        protected override void ExecuteCore()
        {
            AbsolutePath assemblyPath = TaskEnvironment.GetAbsolutePath(IntermediateAssembly);
            AbsolutePath clsidMapPath = TaskEnvironment.GetAbsolutePath(ClsidMapDestinationPath);

            using (var assemblyStream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                try
                {
                    using (PEReader peReader = new(assemblyStream))
                    {
                        if (peReader.HasMetadata)
                        {
                            MetadataReader reader = peReader.GetMetadataReader();
                            if (!reader.IsAssembly)
                            {
                                Log.LogError(Strings.ClsidMapInvalidAssembly, IntermediateAssembly);
                                return;
                            }
                            ClsidMap.Create(reader, clsidMapPath);
                        }
                    }
                }
                catch (MissingGuidException missingGuid)
                {
                    Log.LogError(Strings.ClsidMapExportedTypesRequireExplicitGuid, missingGuid.TypeName);
                }
                catch (ConflictingGuidException conflictingGuid)
                {
                    Log.LogError(Strings.ClsidMapConflictingGuids, conflictingGuid.TypeName1, conflictingGuid.TypeName2, conflictingGuid.Guid.ToString());
                }
                catch (BadImageFormatException)
                {
                    Log.LogError(Strings.ClsidMapInvalidAssembly, IntermediateAssembly);
                    return;
                }
            }
        }
    }
}

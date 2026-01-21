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
        /// <summary>
        /// Gets or sets the task environment for thread-safe operations.
        /// </summary>
        public TaskEnvironment TaskEnvironment { get; set; }

        [Required]
        public string IntermediateAssembly { get; set; }

        [Required]
        public string ClsidMapDestinationPath { get; set; }

        protected override void ExecuteCore()
        {
            string intermediateAssemblyPath = TaskEnvironment?.GetAbsolutePath(IntermediateAssembly) ?? IntermediateAssembly;
            string clsidMapDestinationPath = TaskEnvironment?.GetAbsolutePath(ClsidMapDestinationPath) ?? ClsidMapDestinationPath;

            using (var assemblyStream = new FileStream(intermediateAssemblyPath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
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
                            ClsidMap.Create(reader, clsidMapDestinationPath);
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

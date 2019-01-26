// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Filters the assemblies from the list to keep managed assemblies.
    /// </summary>
    public class FilterManagedAssemblies : TaskBase
    {
        [Required]
        public ITaskItem[] Assemblies { get; set; }

        /// <summary>
        /// A switch to further restrict the output assemblies to those that only contain IL.
        /// </summary>
        public bool RestrictToILOnlyAssemblies { get; set; }

        [Output]
        public ITaskItem[] ManagedAssemblies { get; private set; }

        protected override void ExecuteCore()
        {
            List<ITaskItem> managedAssemblies = new List<ITaskItem>();
            const int IL_ONLY_FLAG = 1;

            foreach (ITaskItem item in Assemblies)
            {
                using (var assemblyStream = new FileStream(item.ItemSpec, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
                {
                    using (PEReader peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen))
                    {
                        // If RestrictToILOnlyAssemblies is false (default case), only check that the PE file is managed code
                        // IF RestrictToILOnlyAssemblies is true, run an additional check that the CorHeader flag is set to IL ONLY
                        if (peReader.HasMetadata &&
                                (!RestrictToILOnlyAssemblies || ((int)peReader.PEHeaders.CorHeader.Flags & IL_ONLY_FLAG) == IL_ONLY_FLAG))
                        {
                            managedAssemblies.Add(item);
                        }
                    }
                }
            }

            ManagedAssemblies = managedAssemblies.ToArray();
        }
    }
}
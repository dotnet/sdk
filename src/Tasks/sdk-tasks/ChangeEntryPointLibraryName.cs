// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public class ChangeEntryPointLibraryName : Task, IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the task environment for thread-safe operations.
        /// </summary>
        public TaskEnvironment TaskEnvironment { get; set; }

        [Required]
        public string DepsFile { get; set; }

        [Required]
        public string NewName { get; set; }

        public override bool Execute()
        {
            // Ensure the path is absolute for thread-safe file operations
            string absoluteDepsFile = TaskEnvironment?.GetAbsolutePath(DepsFile) ?? Path.GetFullPath(DepsFile);
            PublishMutationUtilities.ChangeEntryPointLibraryName(absoluteDepsFile, NewName);

            return true;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Check if Runtime Identifier of the current MSBuild task exists in the targets of the project.assets.json.
    /// Throw an error if RID is not present in the targets.
    /// </summary>
    public sealed class CheckRuntimeIdentifier : TaskBase
    {
        #region Input Items
        /// <summary>
        /// Path to assets.json.
        /// </summary>
        [Required]
        public string ProjectAssetsFile { get; set; }
        /// <summary>
        /// TargetFramework to use for compile-time assets.
        /// </summary>
        [Required]
        public string TargetFramework { get; set; }
        /// <summary>
        /// RID to use for runtime assets.
        /// </summary>
        [Required]
        public string RuntimeIdentifier { get; set; }

        #endregion

        protected override void ExecuteCore()
        {
            var lockFile = new LockFileCache(this).GetLockFile(ProjectAssetsFile);
            var target = lockFile.GetTargetAndReturnNullIfNotFound(TargetFramework, RuntimeIdentifier);
            var targetNotFound = target is null;
            if (targetNotFound)
            {
                var ridMismatchMessage = string.Format(Strings.AssetsFileRuntimeIdentifierMismatch, RuntimeIdentifier);
                throw new BuildErrorException(ridMismatchMessage);
            }
        }
    }
}



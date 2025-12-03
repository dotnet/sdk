// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForTargetInAssetsFile : TaskBase
#if NET10_0_OR_GREATER
    , IMultiThreadableTask
#endif
    {
#if NET10_0_OR_GREATER
        public TaskEnvironment TaskEnvironment { get; set; }
#endif

        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }


        protected override void ExecuteCore()
        {
            LockFile lockFile = new LockFileCache(this).GetLockFile(AssetsFilePath);

            lockFile.GetTargetAndThrowIfNotFound(TargetFramework, RuntimeIdentifier);
        }
    }
}

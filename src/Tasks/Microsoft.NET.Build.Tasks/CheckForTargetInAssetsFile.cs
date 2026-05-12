// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public class CheckForTargetInAssetsFile : TaskBase, IMultiThreadableTask
    {
        #if NETFRAMEWORK
        private TaskEnvironment _taskEnvironment;
        public TaskEnvironment TaskEnvironment
        {
            get => _taskEnvironment ??= TaskEnvironmentDefaults.Create();
            set => _taskEnvironment = value;
        }
        #else
        public TaskEnvironment TaskEnvironment { get; set; }
        #endif

        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }


        protected override void ExecuteCore()
        {
            AbsolutePath assetsFilePath = TaskEnvironment.GetAbsolutePath(AssetsFilePath);
            LockFile lockFile = new LockFileCache(this).GetLockFile(assetsFilePath);

            lockFile.GetTargetAndThrowIfNotFound(TargetFramework, RuntimeIdentifier);
        }
    }
}

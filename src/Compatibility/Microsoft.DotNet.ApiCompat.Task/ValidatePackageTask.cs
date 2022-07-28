// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompat.Task
{
    public class ValidatePackageTask : TaskBase
    {
        [Required]
        public string? PackageTargetPath { get; set; }

        [Required]
        public string? RoslynAssembliesPath { get; set; }

        public string? RuntimeGraph { get; set; }

        public string? NoWarn { get; set; }

        public bool RunApiCompat { get; set; } = true;

        public bool EnableStrictModeForCompatibleTfms { get; set; }

        public bool EnableStrictModeForCompatibleFrameworksInPackage { get; set; }

        public string? BaselinePackageTargetPath { get; set; }

        public bool GenerateCompatibilitySuppressionFile { get; set; }

        public string? CompatibilitySuppressionFilePath { get; set; }

        public ITaskItem[]? PackageAssemblyReferences { get; set; }

        public ITaskItem[]? BaselinePackageAssemblyReferences { get; set; }

        public override bool Execute()
        {
            RoslynResolver.Register(RoslynAssembliesPath);

            try
            {
                return base.Execute();
            }
            finally
            {
                RoslynResolver.Unregister();
            }
        }

        protected override void ExecuteCore()
        {
            Dictionary<string, string[]>? packageAssemblyReferences = null;

            if (PackageAssemblyReferences != null)
            {
                packageAssemblyReferences = new Dictionary<string, string[]>(PackageAssemblyReferences.Length);
                foreach (ITaskItem taskItem in PackageAssemblyReferences)
                {
                    string tfm = taskItem.GetMetadata("Identity");
                    string? referencePath = taskItem.GetMetadata("ReferencePath");
                    if (string.IsNullOrEmpty(referencePath))
                        continue;

                    packageAssemblyReferences.Add(tfm, referencePath.Split(','));
                }
            }

            Func<ISuppressionEngine, MSBuildCompatibilityLogger> logFactory = (suppressionEngine) => new(Log, suppressionEngine);
            ValidatePackage.Run(logFactory,
                GenerateCompatibilitySuppressionFile,
                CompatibilitySuppressionFilePath,
                NoWarn,
                PackageTargetPath!,
                RunApiCompat,
                EnableStrictModeForCompatibleTfms,
                EnableStrictModeForCompatibleFrameworksInPackage,
                BaselinePackageTargetPath,
                RuntimeGraph,
                ParsePackageAssemblyReferences(PackageAssemblyReferences),
                ParsePackageAssemblyReferences(BaselinePackageAssemblyReferences));
        }

        private static Dictionary<string, string[]>? ParsePackageAssemblyReferences(ITaskItem[]? packageAssemblyReferences)
        {
            if (packageAssemblyReferences == null || packageAssemblyReferences.Length == 0)
                return null;

            Dictionary<string, string[]>? packageAssemblyReferencesDict = new(packageAssemblyReferences.Length);
            foreach (ITaskItem taskItem in packageAssemblyReferences)
            {
                string tfm = taskItem.GetMetadata("Identity");
                string? referencePath = taskItem.GetMetadata("ReferencePath");
                if (string.IsNullOrEmpty(referencePath))
                    continue;

                packageAssemblyReferencesDict.Add(tfm, referencePath.Split(','));
            }

            return packageAssemblyReferencesDict;
        }
    }
}

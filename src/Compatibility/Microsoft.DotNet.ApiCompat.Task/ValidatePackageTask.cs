﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompat.Task
{
    /// <summary>
    /// ApiCompat's ValidatePackage msbuild frontend.
    /// This task provides the functionality to compare package assets based on given inputs.
    /// </summary>
    public class ValidatePackageTask : TaskBase
    {
        // Important: Keep properties exposed in sync with the CLI frontend.

        /// <summary>
        /// The path to the package to inspect.
        /// </summary>
        [Required]
        public string? PackageTargetPath { get; set; }

        /// <summary>
        /// If provided, the path to the roslyn assemblies that should be loaded.
        /// </summary>
        public string? RoslynAssembliesPath { get; set; }

        /// <summary>
        /// A runtime graph that can be provided for package asset selection.
        /// </summary>
        public string? RuntimeGraph { get; set; }

        /// <summary>
        /// A NoWarn string contains the error codes that should be ignored.
        /// </summary>
        public string? NoWarn { get; set; }

        /// <summary>
        /// If true, performs api compatibility checks on the package assets.
        /// </summary>
        public bool RunApiCompat { get; set; } = true;

        /// <summary>
        /// Enables strict mode api comparison checks enqueued by the compatible tfm validator.
        /// </summary>
        public bool EnableStrictModeForCompatibleTfms { get; set; }

        /// <summary>
        /// Enables strict mode api comparison checks enqueued by the compatible framework in package validator.
        /// </summary>
        public bool EnableStrictModeForCompatibleFrameworksInPackage { get; set; }

        /// <summary>
        /// Enables strict mode api comparison checks enqueued by the baseline package validator.
        /// </summary>
        public bool EnableStrictModeForBaselineValidation { get; set; }

        /// <summary>
        /// The path to the baseline package that acts as the contract to inspect.
        /// </summary>
        public string? BaselinePackageTargetPath { get; set; }

        /// <summary>
        /// If true, generates a compatibility suppression file that contains the api compatibility errors.
        /// </summary>
        public bool GenerateCompatibilitySuppressionFile { get; set; }

        /// <summary>
        /// The path to a compatibility suppression file. If provided, the suppressions are read and stored.
        /// </summary>
        public string? CompatibilitySuppressionFilePath { get; set; }

        /// <summary>
        /// Assembly references grouped by target framework, for the assets inside the package.
        /// </summary>
        public ITaskItem[]? PackageAssemblyReferences { get; set; }

        /// <summary>
        /// Assembly references grouped by target framework, for the assets inside the baseline package.
        /// </summary>
        public ITaskItem[]? BaselinePackageAssemblyReferences { get; set; }

        public override bool Execute()
        {
            if (RoslynAssembliesPath == null)
            {
                return base.Execute();
            }

            RoslynResolver roslynResolver = RoslynResolver.Register(RoslynAssembliesPath);
            try
            {
                return base.Execute();
            }
            finally
            {
                roslynResolver.Unregister();
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
                EnableStrictModeForBaselineValidation,
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

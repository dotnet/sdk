// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
using Microsoft.DotNet.ApiCompatibility.Logging;

namespace Microsoft.DotNet.ApiCompat.Task
{
    public class ValidateAssembliesTask : TaskBase
    {
        [Required]
        public string? RoslynAssembliesPath { get; set; }

        [Required]
        public string[]? LeftAssemblies { get; set; }

        [Required]
        public string[]? RightAssemblies { get; set; }

        public bool GenerateCompatibilitySuppressionFile { get; set; }

        public string? CompatibilitySuppressionFilePath { get; set; }

        public string? NoWarn { get; set; }

        public bool EnableStrictMode { get; set; }

        public string[]? LeftAssembliesReferences { get; set; }

        public string[]? RightAssembliesReferences { get; set; }

        public string? SemaphoreFile { get; set; }

        public bool CreateWorkItemPerAssembly { get; set; }

        public ITaskItem[]? LeftAssembliesTransformationPattern { get; set; }

        public ITaskItem[]? RightAssembliesTransformationPattern { get; set; }

        public override bool Execute()
        {
            RoslynResolver roslynResolver = RoslynResolver.Register(RoslynAssembliesPath!);

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
            Func<ISuppressionEngine, MSBuildCompatibilityLogger> logFactory = (suppressionEngine) => new(Log, suppressionEngine);
            ValidateAssemblies.Run(logFactory,
                GenerateCompatibilitySuppressionFile,
                CompatibilitySuppressionFilePath,
                NoWarn,
                LeftAssemblies!,
                RightAssemblies!,
                EnableStrictMode,
                ParseAssembliesReferences(LeftAssembliesReferences),
                ParseAssembliesReferences(RightAssembliesReferences),
                CreateWorkItemPerAssembly,
                ParseTransformationPattern(LeftAssembliesTransformationPattern),
                ParseTransformationPattern(RightAssembliesTransformationPattern));

            if (SemaphoreFile != null)
            {
                if (Log.HasLoggedErrors)
                {
                    // To force incremental builds to show failures again, delete the passed in semaphore file.
                    if (File.Exists(SemaphoreFile))
                        File.Delete(SemaphoreFile);
                }
                else
                {
                    // If ValidateAssemblies was successful, create/update the semaphore file.
                    File.Create(SemaphoreFile).Dispose();
                    File.SetLastWriteTimeUtc(SemaphoreFile, DateTime.UtcNow);
                }
            }
        }

        private static string[][]? ParseAssembliesReferences(string[]? assembliesReferences)
        {
            if (assembliesReferences == null || assembliesReferences.Length == 0)
                return null;

            string[][] assembliesReferencesArray = new string[assembliesReferences.Length][];
            for (int i = 0; i < assembliesReferences.Length; i++)
            {
                assembliesReferencesArray[i] = assembliesReferences[i].Split(',');
            }

            return assembliesReferencesArray;
        }

        private static (string CaptureGroupPattern, string ReplacementString)[]? ParseTransformationPattern(ITaskItem[]? transformationPatterns)
        {
            if (transformationPatterns == null)
                return null;

            var patterns = new (string CaptureGroupPattern, string ReplacementPattern)[transformationPatterns.Length];
            for (int i = 0; i < transformationPatterns.Length; i++)
            {
                string captureGroupPattern = transformationPatterns[i].ItemSpec;
                string replacementString = transformationPatterns[i].GetMetadata("ReplacementString");

                if (string.IsNullOrWhiteSpace(replacementString))
                {
                    throw new ArgumentException(string.Format(CommonResources.InvalidRexegStringTransformationPattern, captureGroupPattern, replacementString));
                }

                patterns[i] = (captureGroupPattern, replacementString);
            }

            return patterns;
        }
    }
}

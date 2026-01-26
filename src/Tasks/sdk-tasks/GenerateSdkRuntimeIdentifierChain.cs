// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using NuGet.RuntimeModel;

namespace Microsoft.DotNet.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public class GenerateSdkRuntimeIdentifierChain : Task, IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the task environment for thread-safe operations.
        /// </summary>
        public TaskEnvironment? TaskEnvironment { get; set; }

        [Required]
        public string RuntimeIdentifier { get; set; }

        [Required]
        public string RuntimeIdentifierGraphPath { get; set; }

        [Required]
        public string RuntimeIdentifierChainOutputPath { get; set; }

        public override bool Execute()
        {
            // Ensure paths are absolute for thread-safe file operations
            string graphPath = TaskEnvironment?.GetAbsolutePath(RuntimeIdentifierGraphPath) ?? RuntimeIdentifierGraphPath;
            string outputPath = TaskEnvironment?.GetAbsolutePath(RuntimeIdentifierChainOutputPath) ?? RuntimeIdentifierChainOutputPath;

            // Ensure the output directory exists
            string outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var runtimeIdentifierGraph = JsonRuntimeFormat.ReadRuntimeGraph(graphPath);

            var chainContents = string.Join("\n", runtimeIdentifierGraph.ExpandRuntime(RuntimeIdentifier));
            File.WriteAllText(outputPath, chainContents);

            return true;
        }
    }
}

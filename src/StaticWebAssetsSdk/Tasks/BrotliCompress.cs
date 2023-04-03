// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class BrotliCompress : ToolTask
    {
        private string _dotnetPath;

        [Required]
        public ITaskItem[] FilesToCompress { get; set; }

        public string CompressionLevel { get; set; }

        [Required]
        public string ToolAssembly { get; set; }

        protected override string ToolName => Path.GetDirectoryName(DotNetPath);

        private string DotNetPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_dotnetPath))
                {
                    return _dotnetPath;
                }

                _dotnetPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                if (string.IsNullOrEmpty(_dotnetPath))
                {
                    throw new InvalidOperationException("DOTNET_HOST_PATH is not set");
                }

                return _dotnetPath;
            }
        }

        private static string Quote(string path)
        {
            if (string.IsNullOrEmpty(path) || (path[0] == '\"' && path[path.Length - 1] == '\"'))
            {
                // it's already quoted
                return path;
            }

            return $"\"{path}\"";
        }

        protected override string GenerateCommandLineCommands() => Quote(ToolAssembly);

        protected override string GenerateResponseFileCommands()
        {
            var builder = new StringBuilder();

            builder.AppendLine("brotli");

            if (!string.IsNullOrEmpty(CompressionLevel))
            {
                builder.AppendLine("-c");
                builder.AppendLine(CompressionLevel);
            }

            var outputDirectories = FilesToCompress
                .Select(f => f.GetMetadata("TargetDirectory"))
                .Distinct()
                .Where(td => !string.IsNullOrWhiteSpace(td));

            foreach (var outputDirectory in outputDirectories)
            {
                Directory.CreateDirectory(outputDirectory);
            }

            for (var i = 0; i < FilesToCompress.Length; i++)
            {
                var file = FilesToCompress[i];
                var inputFullPath = file.GetMetadata("RelatedAsset");
                var outputRelativePath = file.ItemSpec;
                var outputFullPath = Path.GetFullPath(outputRelativePath);

                if (!File.Exists(outputRelativePath))
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because compressed file '{1}' does not exist.", inputFullPath, outputRelativePath);
                }
                else if (File.GetLastWriteTimeUtc(inputFullPath) < File.GetLastWriteTimeUtc(outputRelativePath))
                {
                    // Incrementalism. If input source doesn't exist or it exists and is not newer than the expected output, do nothing.
                    Log.LogMessage(MessageImportance.Low, "Skipping '{0}' because '{1}' is newer than '{2}'.", inputFullPath, outputRelativePath, inputFullPath);
                    continue;
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because file is newer than '{1}'.", inputFullPath, outputRelativePath);
                }

                builder.AppendLine("-s");
                builder.AppendLine(Quote(inputFullPath));

                builder.AppendLine("-o");
                builder.AppendLine(Quote(outputFullPath));
            }

            return builder.ToString();
        }

        protected override string GenerateFullPathToTool() => DotNetPath;
    }
}

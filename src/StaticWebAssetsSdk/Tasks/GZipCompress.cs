// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class GZipCompress : Task
    {
        [Required]
        public ITaskItem[] FilesToCompress { get; set; }

        public override bool Execute()
        {
            var outputDirectories = FilesToCompress
                .Select(f => f.GetMetadata("TargetDirectory"))
                .Distinct()
                .Where(td => !string.IsNullOrWhiteSpace(td));

            foreach (var outputDirectory in outputDirectories)
            {
                Directory.CreateDirectory(outputDirectory);
            }

            System.Threading.Tasks.Parallel.For(0, FilesToCompress.Length, i =>
            {
                var file = FilesToCompress[i];
                var inputFullPath = file.GetMetadata("RelatedAsset");
                var outputRelativePath = file.ItemSpec;

                if (!File.Exists(outputRelativePath))
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because compressed file '{1}' does not exist.", inputFullPath, outputRelativePath);
                }
                else if (File.GetLastWriteTimeUtc(inputFullPath) < File.GetLastWriteTimeUtc(outputRelativePath))
                {
                    // Incrementalism. If input source doesn't exist or it exists and is not newer than the expected output, do nothing.
                    Log.LogMessage(MessageImportance.Low, "Skipping '{0}' because '{1}' is newer than '{2}'.", inputFullPath, outputRelativePath, inputFullPath);
                    return;
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because file is newer than '{1}'.", inputFullPath, outputRelativePath);
                }

                try
                {
                    using var sourceStream = File.OpenRead(inputFullPath);
                    using var fileStream = File.Create(outputRelativePath);
                    using var stream = new GZipStream(fileStream, CompressionLevel.Optimal);

                    sourceStream.CopyTo(stream);
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e);
                    return;
                }
            });

            return !Log.HasLoggedErrors;
        }
    }
}

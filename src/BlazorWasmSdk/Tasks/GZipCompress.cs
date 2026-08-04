// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    public class GZipCompress : Task
    {
        [Required]
        public ITaskItem[] FilesToCompress { get; set; }

        [Output]
        public ITaskItem[] CompressedFiles { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        // Retry count for transient file I/O errors (e.g., antivirus locks on CI machines).
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 200;

        public override bool Execute()
        {
            CompressedFiles = new ITaskItem[FilesToCompress.Length];

            Directory.CreateDirectory(OutputDirectory);

            Parallel.For(0, FilesToCompress.Length, i =>
            {
                var file = FilesToCompress[i];
                var inputFullPath = file.GetMetadata("FullPath");
                var relativePath = file.GetMetadata("RelativePath");

                var outputRelativePath = Path.Combine(
                    OutputDirectory,
                    BrotliCompress.CalculateTargetPath(inputFullPath, ".gz"));

                var outputItem = new TaskItem(outputRelativePath, file.CloneCustomMetadata());
                outputItem.SetMetadata("RelativePath", relativePath + ".gz");
                outputItem.SetMetadata("OriginalItemSpec", file.ItemSpec);
                CompressedFiles[i] = outputItem;

                if (!File.Exists(outputRelativePath))
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because compressed file '{1}' does not exist.", file.ItemSpec, outputRelativePath);
                }
                else if (File.GetLastWriteTimeUtc(inputFullPath) < File.GetLastWriteTimeUtc(outputRelativePath))
                {
                    // Incrementalism. If input source doesn't exist or it exists and is not newer than the expected output, do nothing.
                    Log.LogMessage(MessageImportance.Low, "Skipping '{0}' because '{1}' is newer than '{2}'.", file.ItemSpec, outputRelativePath, file.ItemSpec);
                    return;
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because file is newer than '{1}'.", inputFullPath, outputRelativePath);
                }

                // Retry on IOException to handle transient file locks from antivirus, file
                // indexing, or parallel MSBuild nodes on CI machines (see dotnet/sdk#53424).
                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        using var sourceStream = File.OpenRead(file.ItemSpec);
                        using var fileStream = File.Create(outputRelativePath);
                        using var stream = new GZipStream(fileStream, CompressionLevel.Optimal);

                        sourceStream.CopyTo(stream);
                        return; // Success
                    }
                    catch (IOException) when (attempt < MaxRetries)
                    {
                        Log.LogMessage(MessageImportance.Low,
                            "Retrying compression of '{0}' (attempt {1}/{2}) due to transient I/O error.",
                            file.ItemSpec, attempt, MaxRetries);
                        Thread.Sleep(RetryDelayMs * attempt);
                    }
                    catch (Exception e)
                    {
                        Log.LogErrorFromException(e);
                        return;
                    }
                }
            });

            return !Log.HasLoggedErrors;
        }
    }
}

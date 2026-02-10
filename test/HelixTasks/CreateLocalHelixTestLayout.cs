// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    public sealed class CreateLocalHelixTestLayout : Build.Utilities.Task
    {
        [Required]
        public ITaskItem[]? HelixCorrelationPayload { get; set; }

        [Required]
        public string? TestOutputDirectory { get; set; }

        public override bool Execute()
        {
            if (HelixCorrelationPayload is null)
            {
                return false;
            };

            // Clean the test output directory once at the start
            if (Directory.Exists(TestOutputDirectory))
            {
                Directory.Delete(TestOutputDirectory, recursive: true);
            }

            foreach (var payload in HelixCorrelationPayload)
            {
                var sourceItem = payload.ItemSpec;
                var relativeDestinationPathOnHelix = payload.GetMetadata("Destination");
                var destinationDir = Path.Combine(TestOutputDirectory ?? string.Empty, relativeDestinationPathOnHelix);

                if (File.Exists(sourceItem))
                {
                    // It's a file - copy just this file to the destination directory
                    Directory.CreateDirectory(destinationDir);
                    var fileName = Path.GetFileName(sourceItem);
                    File.Copy(sourceItem, Path.Combine(destinationDir, fileName), overwrite: true);
                }
                else if (Directory.Exists(sourceItem))
                {
                    // It's a directory - copy all its contents
                    var source = new DirectoryInfo(sourceItem);
                    var destination = new DirectoryInfo(destinationDir);

                    CopyAll(source, destination);
                }
                else
                {
                    Log.LogWarning($"Payload item '{sourceItem}' does not exist.");
                }
            }
            Log.LogMessage($"set HELIX_CORRELATION_PAYLOAD={TestOutputDirectory}");
            return true;
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            if (source.FullName.ToLower() == target.FullName.ToLower())
            {
                return;
            }

            Directory.CreateDirectory(target.FullName);

            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.ToString(), fi.Name), true);
            }

            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }
    }
}

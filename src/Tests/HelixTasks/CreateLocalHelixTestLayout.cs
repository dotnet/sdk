// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    public sealed class CreateLocalHelixTestLayout : Build.Utilities.Task
    {
        [Required]
        public ITaskItem[] HelixCorrelationPayload { get; set; }

        [Required]
        public string TestOutputDirectory { get; set; }

        public override bool Execute()
        {
            foreach (var payload in HelixCorrelationPayload)
            {
                var copyfrom = new DirectoryInfo(payload.GetMetadata("PayloadDirectory"));
                var relativeDestinationPathOnHelix = payload.GetMetadata("Destination");
                var destination = new DirectoryInfo(Path.Combine(TestOutputDirectory, relativeDestinationPathOnHelix));

                if (Directory.Exists(destination.FullName))
                {
                    Directory.Delete(destination.FullName, true);
                }

                CopyAll(copyfrom, destination);
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

            if (Directory.Exists(target.FullName) == false)
            {
                Directory.CreateDirectory(target.FullName);
            }

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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace Microsoft.DotNet.Build.Tasks
{
    public class WriteVersionsFile : Task
    {
        [Required]
        public ITaskItem[] NugetPackages { get; set; }

        [Required]
        public string OutputPath { get; set; }

        public override bool Execute()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            using (Stream outStream = File.Open(OutputPath, FileMode.Create))
            {
                using (StreamWriter sw = new StreamWriter(outStream, new UTF8Encoding(false)))
                {
                    foreach (ITaskItem nugetPackage in NugetPackages)
                    {
                        using (PackageArchiveReader par = new PackageArchiveReader(nugetPackage.GetMetadata("FullPath")))
                        {
                            PackageIdentity packageIdentity = par.GetIdentity();
                            sw.WriteLine($"{packageIdentity.Id} {packageIdentity.Version}");
                        }
                    }
                }
            }

            return true;
        }
    }
}

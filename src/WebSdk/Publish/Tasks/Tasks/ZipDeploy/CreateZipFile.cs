// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    public class CreateZipFile : Task
    {
        [Required]
        public string FolderToZip { get; set; }

        [Required]
        public string ProjectName { get; set; }

        [Required]
        public string PublishIntermediateTempPath { get; set; }

        [Output]
        public string CreatedZipPath { get; private set; }

        public override bool Execute()
        {
            string zipFileName = ProjectName + "-" + DateTime.Now.ToString("yyyyMMddHHmmssFFF") + ".zip";
            CreatedZipPath = Path.Combine(PublishIntermediateTempPath, zipFileName);
            ZipFile.CreateFromDirectory(FolderToZip, CreatedZipPath);
            return true;
        }
    }
}

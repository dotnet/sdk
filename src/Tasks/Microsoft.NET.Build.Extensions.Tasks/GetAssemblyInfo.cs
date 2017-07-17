// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using System;
using System.Diagnostics;
using System.IO;
using System.Security;

namespace Microsoft.NET.Build.Tasks
{
    public class GetAssemblyInfo: TaskBase
    {
        [Required]
        public string FilePath { get; set; }

        [Output]
        public string FileVersion { get; set; }

        [Output]
        public string AssemblyVersion { get; set; }


        protected override void ExecuteCore()
        {
            GetAssemblyInfoFrom(FilePath);
        }

        private void GetAssemblyInfoFrom(string FilePath)
        {
            FileVersion = FileUtilities.GetFileVersion(FilePath).ToString();
            AssemblyVersion = FileUtilities.GetFileVersion(FilePath).ToString();
        }
    }
}

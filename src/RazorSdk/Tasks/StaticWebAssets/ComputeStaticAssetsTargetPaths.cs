// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class ComputeStaticWebAssetsTargetPaths : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        public string PathPrefix { get; set; }

        [Output]
        public ITaskItem[] AssetsWithTargetPath { get; set; }

        public override bool Execute()
        {
            try
            {
                AssetsWithTargetPath = Assets
                    .Select(StaticWebAsset.FromTaskItem)
                    .Select(a => new TaskItem(a.Identity, new Dictionary<string, string> {
                        ["TargetPath"] = a.ComputeTargetPath(PathPrefix)
                    }))
                    .ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
            }

            return !Log.HasLoggedErrors;
        }
    }
}

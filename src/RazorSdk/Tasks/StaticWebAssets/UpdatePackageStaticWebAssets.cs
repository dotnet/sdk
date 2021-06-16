// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class UpdatePackageStaticWebAssets : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        [Output]
        public ITaskItem[] UpdatedAssets { get; set; }

        public override bool Execute()
        {
            try
            {
                UpdatedAssets = Assets
                    .Select(c => (candidate: c, item: StaticWebAsset.FromV1TaskItem(c)))
                    .Where(a => a.item.SourceType == StaticWebAsset.SourceTypes.Package)
                    .Select(a =>
                    {
                        var result = a.item.ToTaskItem();
                        result.SetMetadata("OriginalItemSpec", a.candidate.ItemSpec);
                        return result;
                    }).ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }
    }
}

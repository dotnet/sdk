// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tasks
{
    internal struct KnownRuntimePack
    {
        ITaskItem _item;

        public KnownRuntimePack(ITaskItem item)
        {
            _item = item;
            TargetFramework = NuGetFramework.Parse(item.GetMetadata("TargetFramework"));
            string runtimePackLabels = item.GetMetadata(MetadataKeys.RuntimePackLabels);
            if (String.IsNullOrEmpty(runtimePackLabels))
            {
                RuntimePackLabels = Array.Empty<string>();
            }
            else
            {
                RuntimePackLabels = runtimePackLabels.Split(';');
            }
        }

        //  The name / itemspec of the FrameworkReference used in the project
        public string Name => _item.ItemSpec;

        ////  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
        public string LatestRuntimeFrameworkVersion => _item.GetMetadata("LatestRuntimeFrameworkVersion");

        public string RuntimePackNamePatterns => _item.GetMetadata("RuntimePackNamePatterns");

        public string RuntimePackRuntimeIdentifiers => _item.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers);

        public string IsTrimmable => _item.GetMetadata(MetadataKeys.IsTrimmable);

        public bool IsWindowsOnly => _item.HasMetadataValue("IsWindowsOnly", "true");

        public bool RuntimePackAlwaysCopyLocal =>
            _item.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

        public string[] RuntimePackLabels { get; }

        public NuGetFramework TargetFramework { get; }
    }
}

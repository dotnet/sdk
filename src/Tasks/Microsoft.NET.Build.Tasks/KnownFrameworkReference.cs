// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal struct KnownFrameworkReference
    {
        ITaskItem _item;
        public KnownFrameworkReference(ITaskItem item)
        {
            _item = item;
            TargetFramework = new ProcessFrameworkReferences.NuGetFrameworkTemp(item.GetMetadata("TargetFramework"));
        }

        //  The name / itemspec of the FrameworkReference used in the project
        public string Name => _item.ItemSpec;

        //  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
        public string RuntimeFrameworkName => _item.GetMetadata(MetadataKeys.RuntimeFrameworkName);
        public string DefaultRuntimeFrameworkVersion => _item.GetMetadata("DefaultRuntimeFrameworkVersion");

        //  The ID of the targeting pack NuGet package to reference
        public string TargetingPackName => _item.GetMetadata("TargetingPackName");
        public string TargetingPackVersion => _item.GetMetadata("TargetingPackVersion");
        public string TargetingPackFormat => _item.GetMetadata("TargetingPackFormat");

        public string RuntimePackRuntimeIdentifiers => _item.GetMetadata(MetadataKeys.RuntimePackRuntimeIdentifiers);

        public bool IsWindowsOnly => _item.HasMetadataValue("IsWindowsOnly", "true");
            
        public bool RuntimePackAlwaysCopyLocal =>
            _item.HasMetadataValue(MetadataKeys.RuntimePackAlwaysCopyLocal, "true");

        public string Profile => _item.GetMetadata("Profile");

        public ProcessFrameworkReferences.NuGetFrameworkTemp TargetFramework { get; }

        public KnownRuntimePack ToKnownRuntimePack()
        {
            return new KnownRuntimePack(_item);
        }

        public bool AppliesTo(
            string targetFrameworkIdentifier,
            string targetFrameworkVersion,
            string targetPlatformVersion)
        {
            var normalizedTargetFrameworkVersion = ProcessFrameworkReferences.NormalizeVersion(new Version(targetFrameworkVersion));
            if (!TargetFramework.Framework.Equals(targetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase)
                || ProcessFrameworkReferences.NormalizeVersion(this.TargetFramework.Version) != normalizedTargetFrameworkVersion)
            {
                return false;
            }

            if (!String.IsNullOrEmpty(TargetFramework.Platform)
                && !String.IsNullOrEmpty(TargetFramework.PlatformVersion))
            {
                if (!TargetFramework.PlatformVersion.Equals(targetPlatformVersion,
                        StringComparison.OrdinalIgnoreCase)
                    || ProcessFrameworkReferences.NormalizeVersion(TargetFramework.Version) != normalizedTargetFrameworkVersion)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

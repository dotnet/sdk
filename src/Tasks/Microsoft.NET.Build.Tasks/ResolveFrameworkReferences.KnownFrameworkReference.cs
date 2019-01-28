using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tasks
{
    internal class KnownFrameworkReference
    {
        ITaskItem _item;
        public KnownFrameworkReference(ITaskItem item)
        {
            _item = item;
            TargetFramework = NuGetFramework.Parse(item.GetMetadata("TargetFramework"));
        }

        //  The name / itemspec of the FrameworkReference used in the project
        public string Name => _item.ItemSpec;

        //  The framework name to write to the runtimeconfig file (and the name of the folder under dotnet/shared)
        public string RuntimeFrameworkName => _item.GetMetadata("RuntimeFrameworkName");
        public string DefaultRuntimeFrameworkVersion => _item.GetMetadata("DefaultRuntimeFrameworkVersion");
        public string LatestRuntimeFrameworkVersion => _item.GetMetadata("LatestRuntimeFrameworkVersion");

        //  The ID of the targeting pack NuGet package to reference
        public string TargetingPackName => _item.GetMetadata("TargetingPackName");
        public string TargetingPackVersion => _item.GetMetadata("TargetingPackVersion");

        public string AppHostPackNamePattern => _item.GetMetadata("AppHostPackNamePattern");

        public string AppHostRuntimeIdentifiers => _item.GetMetadata("AppHostRuntimeIdentifiers");

        public string RuntimePackNamePatterns => _item.GetMetadata("RuntimePackNamePatterns");

        public string RuntimePackRuntimeIdentifiers => _item.GetMetadata("RuntimePackRuntimeIdentifiers");

        public NuGetFramework TargetFramework { get; }

        public static Dictionary<string, KnownFrameworkReference> GetKnownFrameworkReferenceDictionary(
            ITaskItem[] knownFrameworkReferences,
            string targetFrameworkIdentifier,
            string targetFrameworkVersion)
        {
            return knownFrameworkReferences.Select(item => new KnownFrameworkReference(item))
            .Where(kfr => kfr.TargetFramework.Framework.Equals(targetFrameworkIdentifier, StringComparison.OrdinalIgnoreCase) &&
                          NormalizeVersion(kfr.TargetFramework.Version) == NormalizeVersion(new Version(targetFrameworkVersion)))
            .ToDictionary(kfr => kfr.Name);
        }

        private static Version NormalizeVersion(Version version)
        {
            if (version.Revision == 0)
            {
                if (version.Build == 0)
                {
                    return new Version(version.Major, version.Minor);
                }
                else
                {
                    return new Version(version.Major, version.Minor, version.Build);
                }
            }

            return version;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolveFrameworkReferences : TaskBase
    {
        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ResolvedTargetingPacks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ResolvedRuntimePacks { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] ResolvedFrameworkReferences { get; set; }

        protected override void ExecuteCore()
        {
            if (FrameworkReferences.Length == 0)
            {
                return;
            }

            var resolvedTargetingPacks = ResolvedTargetingPacks.ToDictionary(tp => tp.ItemSpec, StringComparer.OrdinalIgnoreCase);

            var resolvedFrameworkReferences = new List<TaskItem>(FrameworkReferences.Length);

            foreach (var frameworkReference in FrameworkReferences)
            {
                ITaskItem targetingPack;
                if (!resolvedTargetingPacks.TryGetValue(frameworkReference.ItemSpec, out targetingPack))
                {
                    //  FrameworkReference didn't resolve to a targeting pack
                    continue;
                }

                TaskItem resolvedFrameworkReference = new TaskItem(frameworkReference.ItemSpec);
                resolvedFrameworkReference.SetMetadata(MetadataKeys.OriginalItemSpec, frameworkReference.ItemSpec);
                resolvedFrameworkReference.SetMetadata(MetadataKeys.IsImplicitlyDefined, frameworkReference.GetMetadata(MetadataKeys.IsImplicitlyDefined));

                resolvedFrameworkReference.SetMetadata("TargetingPackPath", targetingPack.GetMetadata(MetadataKeys.Path));
                resolvedFrameworkReference.SetMetadata("TargetingPackName", targetingPack.GetMetadata(MetadataKeys.NuGetPackageId));
                resolvedFrameworkReference.SetMetadata("TargetingPackVersion", targetingPack.GetMetadata(MetadataKeys.NuGetPackageVersion));
                resolvedFrameworkReference.SetMetadata("Profile", targetingPack.GetMetadata("Profile"));

                // Allow more than one runtime pack to be associated with this FrameworkReference
                var matchingRuntimePacks = ResolvedRuntimePacks.Where(rp => rp.GetMetadata(MetadataKeys.FrameworkName).Equals(frameworkReference.ItemSpec, StringComparison.OrdinalIgnoreCase));
                if (matchingRuntimePacks.Any())
                {
                    resolvedFrameworkReference.SetMetadata("RuntimePackPath", string.Join (";", matchingRuntimePacks.Select (mrp => mrp.GetMetadata(MetadataKeys.PackageDirectory))));
                    resolvedFrameworkReference.SetMetadata("RuntimePackName", string.Join (";", matchingRuntimePacks.Select (mrp => mrp.GetMetadata(MetadataKeys.NuGetPackageId))));
                    resolvedFrameworkReference.SetMetadata("RuntimePackVersion", string.Join (";", matchingRuntimePacks.Select (mrp => mrp.GetMetadata(MetadataKeys.NuGetPackageVersion))));
                 }

                resolvedFrameworkReferences.Add(resolvedFrameworkReference);
            }

            ResolvedFrameworkReferences = resolvedFrameworkReferences.ToArray();
        }
    }
}

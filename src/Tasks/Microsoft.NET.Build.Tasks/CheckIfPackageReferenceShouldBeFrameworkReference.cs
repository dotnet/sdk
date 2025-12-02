// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class CheckIfPackageReferenceShouldBeFrameworkReference : TaskBase
#if NET10_0_OR_GREATER
    , IMultiThreadableTask
#endif
    {
        public ITaskItem[] PackageReferences { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] FrameworkReferences { get; set; } = Array.Empty<ITaskItem>();

        public string PackageReferenceToReplace { get; set; }

        public string FrameworkReferenceToUse { get; set; }

        [Output]
        public bool ShouldRemovePackageReference { get; set; }

        [Output]
        public bool ShouldAddFrameworkReference { get; set; }

#if NET10_0_OR_GREATER
        public TaskEnvironment TaskEnvironment { get; set; }
#endif

        protected override void ExecuteCore()
        {
            foreach (var packageReference in PackageReferences)
            {
                if (packageReference.ItemSpec.Equals(PackageReferenceToReplace, StringComparison.OrdinalIgnoreCase))
                {
                    ShouldRemovePackageReference = true;
                    if (!FrameworkReferences.Any(fr => fr.ItemSpec.Equals(FrameworkReferenceToUse, StringComparison.OrdinalIgnoreCase)))
                    {
                        ShouldAddFrameworkReference = true;
                    }
                }
            }
        }
    }
}

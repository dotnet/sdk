// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GenerateDefaultRuntimeFrameworkVersion : Task
    {
        [Required]
        public string RuntimePackVersion { get; set; }

        [Output]
        public string DefaultRuntimeFrameworkVersion { get; set; }

        public override bool Execute()
        {
            if (NuGetVersion.TryParse(RuntimePackVersion, out var version))
            {
                DefaultRuntimeFrameworkVersion = version.IsPrerelease && version.Patch == 0 ?
                    RuntimePackVersion :
                    new NuGetVersion(version.Major, version.Minor, 0).ToFullString();

                return true;
            }

            return false;
        }
    }
}

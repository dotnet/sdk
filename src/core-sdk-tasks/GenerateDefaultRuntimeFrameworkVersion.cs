// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Build
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.Build
{
    public class GetLinuxNativeInstallerDependencyVersions : Task
    {
        [Required]
        public string PackageVersion { get; set; }

        [Output]
        public string MajorMinorVersion { get; private set; }

        [Output]
        public string MajorMinorPatchVersion { get; private set; }

        [Output]
        public string VersionWithTilde { get; private set; }

        public override bool Execute()
        {
            string[] dotSplit = PackageVersion.Split('.');
            MajorMinorVersion = dotSplit[0] + "." + dotSplit[1];

            string[] prereleaseSplit = PackageVersion.Split(new[] { '-' }, count: 2);
            MajorMinorPatchVersion = prereleaseSplit[0];
            VersionWithTilde = MajorMinorPatchVersion;

            if (prereleaseSplit.Length > 1)
            {
                VersionWithTilde += "~" + prereleaseSplit[1];
            }

            return true;
        }
    }
}

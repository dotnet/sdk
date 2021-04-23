// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.PackageValidation
{
    public class ValidatePackage : TaskBase
    {
        [Required]
        public string PackagePath { get; set; }

        [Required]
        public string PreviousPackagePath { get; set; }

        public string RuntimeGraph { get; set; }

        public string NoWarn { get; set; }

        public bool RunApiCompat { get; set; }

        public bool ValidateWithPreviousVersion { get; set; }

        protected override void ExecuteCore()
        {
            if (!Debugger.IsAttached) { Debugger.Launch(); } else { Debugger.Break(); }
            Package package = NupkgParser.CreatePackage(PackagePath, RuntimeGraph);
            Package previousPackage = NupkgParser.CreatePackage(PreviousPackagePath, RuntimeGraph);
            PackageValidationLogger log = new PackageValidationLogger();

            new CompatibleTfmValidator(NoWarn, null, RunApiCompat, log).Validate(package);
            new PreviousVersionValidator(previousPackage, NoWarn, null, RunApiCompat, log).Validate(package);
            new CompatibleFrameworkInPackageValidator(NoWarn, null, log).Validate(package);
        }

    }
}

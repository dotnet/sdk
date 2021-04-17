// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.NET.Build.Tasks;
using System.Collections.Generic;

namespace Microsoft.DotNet.PackageValidation
{
    public class ValidatePackage : TaskBase
    {
        [Required]
        public string PackagePath { get; set; }

        public string RuntimeGraph { get; set; }

        public string NoWarn { get; set; }

        public bool RunApiCompat { get; set; }

        public bool ValidateWithPreviousVersion { get; set; }

        protected override void ExecuteCore()
        {
            if (!File.Exists(PackagePath))
            {
                Log.LogError(string.Format(Resources.InvalidPackagePath, PackagePath));
                return;
            }

            Package package = NupkgParser.CreatePackage(PackagePath, RuntimeGraph);

            // TODO: Add support for generating the baseline differences.
            DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> packageValidationErrors = new CompileTimeValidator(NoWarn, null, RunApiCompat).Validate(package);
            packageValidationErrors.Add(new CompatibleTFMValidator(NoWarn, null, RunApiCompat).Validate(package));
            packageValidationErrors.Add(new CompatibleFrameworkInPackageValidator(NoWarn, null).Validate(package));
            packageValidationErrors.Add(new PreviousVersionValidator(package, NoWarn, null, RunApiCompat).Validate(package));

            foreach (var error in packageValidationErrors.Differences)
            {
                Log.LogError(error.ToString());
            }

            // TODO: Finalize the format.
            IEnumerable<CompatDifference> apiCompatErrors = ApiCompatRunner.RunApiCompat();
            foreach (var error in apiCompatErrors)
            {
                Log.LogError(error.ToString());
            }
        }
    }
}

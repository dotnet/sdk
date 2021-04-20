// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.NET.Build.Tasks;

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
            Package package = NupkgParser.CreatePackage(PackagePath, RuntimeGraph);

            LogErrors(new CompatibleTfmValidator(NoWarn, null, RunApiCompat).Validate(package));
            LogErrors(new PreviousVersionValidator(package, NoWarn, null, RunApiCompat).Validate(package));

            foreach (var apiCompatError in new CompatibleFrameworkInPackageValidator(NoWarn, null).Validate(package))
            {
                Log.LogError(apiCompatError.CompatibilityReason);
                Log.LogError(apiCompatError.Header);
                foreach (var error in apiCompatError.Differences.Differences)
                {
                    Log.LogError(error.ToString());
                }
            }
        }

        private void LogErrors((DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> packageValidationErrors, IEnumerable<ApiCompatDiagnostics> apiCompaterrors) errors)
        {
            foreach (var error in errors.packageValidationErrors.Differences)
            {
                Log.LogError(error.ToString());
            }

            foreach (var apiCompatError in errors.apiCompaterrors)
            {
                Log.LogError(apiCompatError.CompatibilityReason);
                Log.LogError(apiCompatError.Header);
                foreach (var error in apiCompatError.Differences.Differences)
                {
                    Log.LogError(error.ToString());
                }
            }
        }
    }
}

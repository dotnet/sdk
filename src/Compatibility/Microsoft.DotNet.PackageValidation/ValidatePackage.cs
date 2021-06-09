// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.DotNet.ValidationSuppression;
using Microsoft.NET.Build.Tasks;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.PackageValidation
{
    public class ValidatePackage : TaskBase
    {
        [Required]
        public string PackageTargetPath { get; set; }

        public string RuntimeGraph { get; set; }

        public string NoWarn { get; set; }

        public bool RunApiCompat { get; set; }

        public string BaselinePackageTargetPath { get; set; }

        public bool BaselineAllValidationErrors { get; set; }

        public string ValidationSuppressionBaselinePath { get; set; }

        protected override void ExecuteCore()
        {
            Debugger.Launch();
            
            RuntimeGraph runtimeGraph = null;
            if (!string.IsNullOrEmpty(RuntimeGraph))
            {
                runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(RuntimeGraph);
            }

            Package package = NupkgParser.CreatePackage(PackageTargetPath, runtimeGraph);
            PackageValidationLogger logger = new(Log, ValidationSuppressionBaselinePath, BaselineAllValidationErrors);

            new CompatibleTfmValidator(NoWarn, null, RunApiCompat, logger).Validate(package);
            new CompatibleFrameworkInPackageValidator(NoWarn, null, logger).Validate(package);
            if (!string.IsNullOrEmpty(BaselinePackageTargetPath))
            {
                Package baselinePackage = NupkgParser.CreatePackage(BaselinePackageTargetPath, runtimeGraph);
                new BaselinePackageValidator(baselinePackage, NoWarn, null, RunApiCompat, logger).Validate(package);
            }

            if (BaselineAllValidationErrors)
            {
                logger.BaselineAllSuppressionsToFile(ValidationSuppressionBaselinePath);
            }
        }
    }
}

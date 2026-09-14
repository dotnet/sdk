// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Filtering;
using Microsoft.DotNet.PackageValidation.Validators;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class ValidatePackage
    {
        public static int Run(Func<ISuppressionEngine, ISuppressibleLog> logFactory,
            ValidatePackageOptions options)
        {
            // Initialize the service provider
            ApiCompatServiceProvider serviceProvider = new(logFactory,
                () => SuppressionFileHelper.CreateSuppressionEngine(options.SuppressionFiles, options.NoWarn, options.GenerateSuppressionFile),
                (log) => new RuleFactory(log,
                    options.EnableRuleAttributesMustMatch,
                    options.EnableRuleCannotChangeParameterName),
                options.RespectInternals,
                options.ExcludeAttributesFiles);

            // If a runtime graph is provided, parse and use it for asset selection during the in-memory package construction.
            if (options.RuntimeGraph != null)
            {
                Package.InitializeRuntimeGraph(options.RuntimeGraph);
            }

            // Create the in-memory representation of the passed in package path
            Package package = Package.Create(options.PackagePath, options.PackageAssemblyReferences);

            // Invoke all validators and pass the specific validation options in. Don't execute work items, just enqueue them.
            CompatibleTfmValidator tfmValidator = new(serviceProvider.SuppressibleLog, serviceProvider.ApiCompatRunner);
            tfmValidator.Validate(new PackageValidatorOption(package,
                options.EnableStrictModeForCompatibleTfms,
                enqueueApiCompatWorkItems: options.RunApiCompat,
                executeApiCompatWorkItems: false));

            CompatibleFrameworkInPackageValidator compatibleFrameworkInPackageValidator = new(serviceProvider.SuppressibleLog, serviceProvider.ApiCompatRunner);
            compatibleFrameworkInPackageValidator.Validate(new PackageValidatorOption(package,
                options.EnableStrictModeForCompatibleFrameworksInPackage,
                enqueueApiCompatWorkItems: options.RunApiCompat,
                executeApiCompatWorkItems: false));

            if (!string.IsNullOrEmpty(options.BaselinePackagePath))
            {
                BaselinePackageValidator baselineValidator = new(serviceProvider.SuppressibleLog, serviceProvider.ApiCompatRunner);
                baselineValidator.Validate(new PackageValidatorOption(package,
                    enableStrictMode: options.EnableStrictModeForBaselineValidation,
                    enqueueApiCompatWorkItems: options.RunApiCompat,
                    executeApiCompatWorkItems: false,
                    Package.Create(options.BaselinePackagePath, options.BaselinePackageAssemblyReferences),
                    options.BaselinePackageFrameworksToIgnore is not null ? new TargetFrameworkFilter(options.BaselinePackageFrameworksToIgnore) : null));
            }

            if (options.RunApiCompat)
            {
                // Execute the work items that were enqueued.
                serviceProvider.ApiCompatRunner.ExecuteWorkItems();

                SuppressionFileHelper.LogApiCompatSuccessOrFailure(options.GenerateSuppressionFile, serviceProvider.SuppressibleLog);
            }

            if (options.GenerateSuppressionFile)
            {
                SuppressionFileHelper.GenerateSuppressionFile(serviceProvider.SuppressionEngine,
                    serviceProvider.SuppressibleLog,
                    options.PreserveUnnecessarySuppressions,
                    options.SuppressionFiles,
                    options.SuppressionOutputFile);
            }
            else if (!options.PermitUnnecessarySuppressions)
            {
                SuppressionFileHelper.ValidateUnnecessarySuppressions(serviceProvider.SuppressionEngine, serviceProvider.SuppressibleLog);
            }

            return serviceProvider.SuppressibleLog.HasLoggedErrorSuppressions ? 1 : 0;
        }
    }
}

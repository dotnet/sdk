// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Filtering;
using Microsoft.DotNet.PackageValidation.Validators;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class ValidatePackage
    {
        public static int Run(Func<ISuppressionEngine, ISuppressibleLog> logFactory,
            bool generateSuppressionFile,
            bool preserveUnnecessarySuppressions,
            bool permitUnnecessarySuppressions,
            string[]? suppressionFiles,
            string? suppressionOutputFile,
            string? noWarn,
            bool respectInternals,
            bool enableRuleAttributesMustMatch,
            string[]? excludeAttributesFiles,
            bool enableRuleCannotChangeParameterName,
            string neutralLanguage,
            string? packagePath,
            bool runApiCompat,
            bool enableStrictModeForCompatibleTfms,
            bool enableStrictModeForCompatibleFrameworksInPackage,
            bool enableStrictModeForBaselineValidation,
            string? baselinePackagePath,
            string? runtimeGraph,
            IReadOnlyDictionary<NuGetFramework, IEnumerable<string>>? packageAssemblyReferences,
            IReadOnlyDictionary<NuGetFramework, IEnumerable<string>>? baselinePackageAssemblyReferences,
            string[]? baselinePackageFrameworksToIgnore)
        {
            // When generating the suppression file and baselining all errors, change the resource language
            // to neutral. This guarantees that suppression files aren't language specific.
            if (generateSuppressionFile)
            {
                CultureInfo neutralLanguageCultureInfo = new(neutralLanguage);
                Resources.Culture = neutralLanguageCultureInfo;
                CommonResources.Culture = neutralLanguageCultureInfo;
                PackageValidation.ResourceSingleton.ChangeCulture(neutralLanguageCultureInfo);
            }

            // If a runtime graph is provided, parse and use it for asset selection during the in-memory package construction.
            if (runtimeGraph != null)
            {
                Package.InitializeRuntimeGraph(runtimeGraph);
            }

            // Initialize the service provider
            ApiCompatServiceProvider serviceProvider = new(logFactory,
                () => SuppressionFileHelper.CreateSuppressionEngine(suppressionFiles, noWarn, generateSuppressionFile),
                (log) => new RuleFactory(log,
                    enableRuleAttributesMustMatch,
                    enableRuleCannotChangeParameterName),
                respectInternals,
                excludeAttributesFiles);

            // Create the in-memory representation of the passed in package path
            Package package = Package.Create(packagePath, packageAssemblyReferences);

            // Invoke all validators and pass the specific validation options in. Don't execute work items, just enqueue them.
            CompatibleTfmValidator tfmValidator = new(serviceProvider.SuppressibleLog, serviceProvider.ApiCompatRunner);
            tfmValidator.Validate(new PackageValidatorOption(package,
                enableStrictModeForCompatibleTfms,
                enqueueApiCompatWorkItems: runApiCompat,
                executeApiCompatWorkItems: false));

            CompatibleFrameworkInPackageValidator compatibleFrameworkInPackageValidator = new(serviceProvider.SuppressibleLog, serviceProvider.ApiCompatRunner);
            compatibleFrameworkInPackageValidator.Validate(new PackageValidatorOption(package,
                enableStrictModeForCompatibleFrameworksInPackage,
                enqueueApiCompatWorkItems: runApiCompat,
                executeApiCompatWorkItems: false));

            if (!string.IsNullOrEmpty(baselinePackagePath))
            {
                BaselinePackageValidator baselineValidator = new(serviceProvider.SuppressibleLog, serviceProvider.ApiCompatRunner);
                baselineValidator.Validate(new PackageValidatorOption(package,
                    enableStrictMode: enableStrictModeForBaselineValidation,
                    enqueueApiCompatWorkItems: runApiCompat,
                    executeApiCompatWorkItems: false,
                    Package.Create(baselinePackagePath, baselinePackageAssemblyReferences),
                    baselinePackageFrameworksToIgnore is not null ? new TargetFrameworkFilter(baselinePackageFrameworksToIgnore) : null));
            }

            if (runApiCompat)
            {
                // Execute the work items that were enqueued.
                serviceProvider.ApiCompatRunner.ExecuteWorkItems();

                SuppressionFileHelper.LogApiCompatSuccessOrFailure(generateSuppressionFile, serviceProvider.SuppressibleLog);
            }

            if (generateSuppressionFile)
            {
                SuppressionFileHelper.GenerateSuppressionFile(serviceProvider.SuppressionEngine,
                    serviceProvider.SuppressibleLog,
                    preserveUnnecessarySuppressions,
                    suppressionFiles,
                    suppressionOutputFile);
            }
            else if (!permitUnnecessarySuppressions)
            {
                SuppressionFileHelper.ValidateUnnecessarySuppressions(serviceProvider.SuppressionEngine, serviceProvider.SuppressibleLog);
            }

            return serviceProvider.SuppressibleLog.HasLoggedErrorSuppressions ? 1 : 0;
        }
    }
}

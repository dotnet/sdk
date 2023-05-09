﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Validators;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class ValidatePackage
    {
        public static void Run(Func<ISuppressionEngine, ISuppressableLog> logFactory,
            bool generateSuppressionFile,
            string[]? suppressionFiles,
            string? suppressionOutputFile,
            string? noWarn,
            bool respectInternals,
            bool enableRuleAttributesMustMatch,
            string[]? excludeAttributesFiles,
            bool enableRuleCannotChangeParameterName,
            string packagePath,
            bool runApiCompat,
            bool enableStrictModeForCompatibleTfms,
            bool enableStrictModeForCompatibleFrameworksInPackage,
            bool enableStrictModeForBaselineValidation,
            string? baselinePackagePath,
            string? runtimeGraph,
            Dictionary<string, string[]>? packageAssemblyReferences,
            Dictionary<string, string[]>? baselinePackageAssemblyReferences)
        {
            // Initialize the service provider
            ApiCompatServiceProvider serviceProvider = new(logFactory,
                () => new SuppressionEngine(suppressionFiles, noWarn, generateSuppressionFile),
                (log) => new RuleFactory(log,
                    enableRuleAttributesMustMatch,
                    enableRuleCannotChangeParameterName),
                respectInternals,
                excludeAttributesFiles);

            // If a runtime graph is provided, parse and use it for asset selection during the in-memory package construction.
            if (runtimeGraph != null)
            {
                Package.InitializeRuntimeGraph(runtimeGraph);
            }

            // Create the in-memory representation of the passed in package path
            Package package = Package.Create(packagePath, packageAssemblyReferences);

            // Invoke all validators and pass the specific validation options in. Don't execute work items, just enqueue them.
            CompatibleTfmValidator tfmValidator = new(serviceProvider.SuppressableLog, serviceProvider.ApiCompatRunner);
            tfmValidator.Validate(new PackageValidatorOption(package,
                enableStrictModeForCompatibleTfms,
                enqueueApiCompatWorkItems: runApiCompat,
                executeApiCompatWorkItems: false));

            CompatibleFrameworkInPackageValidator compatibleFrameworkInPackageValidator = new(serviceProvider.SuppressableLog, serviceProvider.ApiCompatRunner);
            compatibleFrameworkInPackageValidator.Validate(new PackageValidatorOption(package,
                enableStrictModeForCompatibleFrameworksInPackage,
                enqueueApiCompatWorkItems: runApiCompat,
                executeApiCompatWorkItems: false));

            if (!string.IsNullOrEmpty(baselinePackagePath))
            {
                BaselinePackageValidator baselineValidator = new(serviceProvider.SuppressableLog, serviceProvider.ApiCompatRunner);
                baselineValidator.Validate(new PackageValidatorOption(package,
                    enableStrictMode: enableStrictModeForBaselineValidation,
                    enqueueApiCompatWorkItems: runApiCompat,
                    executeApiCompatWorkItems: false,
                    baselinePackage: Package.Create(baselinePackagePath, baselinePackageAssemblyReferences)));
            }

            if (runApiCompat)
            {
                // Execute the work items that were enqueued.
                serviceProvider.ApiCompatRunner.ExecuteWorkItems();

                SuppressionFileHelper.LogApiCompatSuccessOrFailure(generateSuppressionFile, serviceProvider.SuppressableLog);
            }

            if (generateSuppressionFile)
            {
                SuppressionFileHelper.GenerateSuppressionFile(serviceProvider.SuppressionEngine,
                    serviceProvider.SuppressableLog,
                    suppressionFiles,
                    suppressionOutputFile);
            }
        }
    }
}

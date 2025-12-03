// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.PackageValidation.Filtering;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// A package validator option bag that is passed into package validators for configuration.
    /// </summary>
    /// <remarks>
    /// Instantiates a new PackageValidatorOption type to be passed into a validator.
    /// </remarks>
    public readonly struct PackageValidatorOption(Package package,
        bool enableStrictMode = false,
        bool enqueueApiCompatWorkItems = true,
        bool executeApiCompatWorkItems = true,
        Package? baselinePackage = null,
        ITargetFrameworkFilter? targetFrameworkFilter = null)
    {
        /// <summary>
        /// The latest package that should be validated.
        /// </summary>
        public Package Package { get; } = package;

        /// <summary>
        /// If true, comparison is performed in strict mode.
        /// </summary>
        public bool EnableStrictMode { get; } = enableStrictMode;

        /// <summary>
        /// If true, work items for api compatibility checks are enqueued.
        /// </summary>
        public bool EnqueueApiCompatWorkItems { get; } = enqueueApiCompatWorkItems;

        /// <summary>
        /// If true, executes enqueued api compatibility work items.
        /// </summary>
        public bool ExecuteApiCompatWorkItems { get; } = executeApiCompatWorkItems;

        /// <summary>
        /// The baseline package to validate the latest package.
        /// </summary>
        public Package? BaselinePackage { get; } = baselinePackage;

        /// <summary>
        /// A filter to ignore target frameworks from the baseline package.
        /// Comparison is performed with invariant culture and ignored casing.
        /// </summary>
        public ITargetFrameworkFilter? BaselinePackageFrameworkFilter { get; } = targetFrameworkFilter;
    }
}

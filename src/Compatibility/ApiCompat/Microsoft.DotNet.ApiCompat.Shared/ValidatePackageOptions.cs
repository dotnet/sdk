// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;

namespace Microsoft.DotNet.ApiCompat
{
    /// <summary>
    /// Options for running <see cref="ValidatePackage"/>.
    /// </summary>
    internal sealed class ValidatePackageOptions
    {
        public ValidatePackageOptions(string packagePath)
        {
            PackagePath = packagePath ?? throw new ArgumentNullException(nameof(packagePath));
        }

        /// <summary>
        /// The path to the package to inspect.
        /// </summary>
        public string PackagePath { get; }

        /// <summary>
        /// If true, generates a suppression file that contains the api compatibility errors.
        /// </summary>
        public bool GenerateSuppressionFile { get; set; }

        /// <summary>
        /// If true, preserves unnecessary suppressions when re-generating the suppression file.
        /// </summary>
        public bool PreserveUnnecessarySuppressions { get; set; }

        /// <summary>
        /// If true, permits unnecessary suppressions in the suppression file.
        /// </summary>
        public bool PermitUnnecessarySuppressions { get; set; }

        /// <summary>
        /// The path to one or more suppression files to read from.
        /// </summary>
        public string[]? SuppressionFiles { get; set; }

        /// <summary>
        /// The path to a suppression output file to write to when <see cref="GenerateSuppressionFile"/> is true.
        /// </summary>
        public string? SuppressionOutputFile { get; set; }

        /// <summary>
        /// A NoWarn string that allows disabling specific rules.
        /// </summary>
        public string? NoWarn { get; set; }

        /// <summary>
        /// If true, includes both internal and public API.
        /// </summary>
        public bool RespectInternals { get; set; }

        /// <summary>
        /// Enables rule to check that attributes match.
        /// </summary>
        public bool EnableRuleAttributesMustMatch { get; set; }

        /// <summary>
        /// The path to one or more attribute exclusion files with types in DocId format.
        /// </summary>
        public string[]? ExcludeAttributesFiles { get; set; }

        /// <summary>
        /// Enables rule to check that the parameter names between public methods do not change.
        /// </summary>
        public bool EnableRuleCannotChangeParameterName { get; set; }

        /// <summary>
        /// If true, performs api compatibility checks on the package assets.
        /// </summary>
        public bool RunApiCompat { get; set; } = true;

        /// <summary>
        /// Validates api compatibility in strict mode for contract and implementation assemblies for all compatible target frameworks.
        /// </summary>
        public bool EnableStrictModeForCompatibleTfms { get; set; } = true;

        /// <summary>
        /// Validates api compatibility in strict mode for assemblies that are compatible based on their target framework.
        /// </summary>
        public bool EnableStrictModeForCompatibleFrameworksInPackage { get; set; }

        /// <summary>
        /// Validates api compatibility in strict mode for package baseline checks.
        /// </summary>
        public bool EnableStrictModeForBaselineValidation { get; set; }

        /// <summary>
        /// The path to a baseline package to validate against the current package.
        /// </summary>
        public string? BaselinePackagePath { get; set; }

        /// <summary>
        /// A runtime graph that can be provided for package asset selection.
        /// </summary>
        public string? RuntimeGraph { get; set; }

        /// <summary>
        /// Assembly references grouped by target framework, for the assets inside the package.
        /// </summary>
        public IReadOnlyDictionary<NuGetFramework, IEnumerable<string>>? PackageAssemblyReferences { get; set; }

        /// <summary>
        /// Assembly references grouped by target framework, for the assets inside the baseline package.
        /// </summary>
        public IReadOnlyDictionary<NuGetFramework, IEnumerable<string>>? BaselinePackageAssemblyReferences { get; set; }

        /// <summary>
        /// Target frameworks to ignore from the baseline package.
        /// </summary>
        public string[]? BaselinePackageFrameworksToIgnore { get; set; }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompat
{
    /// <summary>
    /// Options for running <see cref="ValidateAssemblies"/>.
    /// </summary>
    internal sealed class ValidateAssembliesOptions
    {
        public ValidateAssembliesOptions(string[] leftAssemblies, string[] rightAssemblies)
        {
            LeftAssemblies = leftAssemblies ?? throw new ArgumentNullException(nameof(leftAssemblies));
            RightAssemblies = rightAssemblies ?? throw new ArgumentNullException(nameof(rightAssemblies));
        }

        /// <summary>
        /// The assemblies that represent the contract (left side).
        /// </summary>
        public string[] LeftAssemblies { get; }

        /// <summary>
        /// The assemblies that represent the implementation (right side).
        /// </summary>
        public string[] RightAssemblies { get; }

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
        /// Performs api comparison checks in strict mode.
        /// </summary>
        public bool EnableStrictMode { get; set; }

        /// <summary>
        /// The left assemblies' references. The index in the array maps to the index of the passed in left assembly.
        /// </summary>
        public string[][]? LeftAssembliesReferences { get; set; }

        /// <summary>
        /// The right assemblies' references. The index in the array maps to the index of the passed in right assembly.
        /// </summary>
        public string[][]? RightAssembliesReferences { get; set; }

        /// <summary>
        /// Create dedicated api compatibility checks for each left and right assembly tuple.
        /// </summary>
        public bool CreateWorkItemPerAssembly { get; set; }

        /// <summary>
        /// Regex transformation patterns (regex + replacement string) that transform left assembly paths.
        /// </summary>
        public (string CaptureGroupPattern, string ReplacementString)[]? LeftAssembliesTransformationPatterns { get; set; }

        /// <summary>
        /// Regex transformation patterns (regex + replacement string) that transform right assembly paths.
        /// </summary>
        public (string CaptureGroupPattern, string ReplacementString)[]? RightAssembliesTransformationPatterns { get; set; }
    }
}

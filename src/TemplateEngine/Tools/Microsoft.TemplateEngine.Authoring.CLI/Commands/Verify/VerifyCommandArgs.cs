// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;

namespace Microsoft.TemplateEngine.Authoring.CLI.Commands.Verify
{
    /// <summary>
    /// Model class representing the arguments of <see cref="VerifyCommand"/>.
    /// </summary>
    internal sealed class VerifyCommandArgs
    {
        public VerifyCommandArgs(string templateName, string? templateSpecificArgs)
        {
            TemplateName = templateName;
            TemplateSpecificArgs = TokenizeJoinedArgs(templateSpecificArgs);
        }

        public VerifyCommandArgs(
            string? templateName,
            string? templatePath,
            string? templateSpecificArgs,
            string? snapshotsDirectory,
            string? scenarioDistinguisher,
            string? outputDirectory,
            bool disableDiffTool,
            bool disableDefaultVerificationExcludePatterns,
            IEnumerable<string>? verificationExcludePatterns,
            IEnumerable<string>? verificationIncludePatterns,
            bool verifyCommandOutput,
            bool isCommandExpectedToFail,
            IEnumerable<UniqueForOption>? uniqueForOptions)
        : this(templateName ?? string.Empty, templateSpecificArgs)
        {
            TemplatePath = templatePath;
            SnapshotsDirectory = snapshotsDirectory;
            ScenarioDistinguisher = scenarioDistinguisher;
            OutputDirectory = outputDirectory;
            DisableDiffTool = disableDiffTool;
            DisableDefaultVerificationExcludePatterns = disableDefaultVerificationExcludePatterns;
            VerificationExcludePatterns = verificationExcludePatterns;
            VerificationIncludePatterns = verificationIncludePatterns;
            VerifyCommandOutput = verifyCommandOutput;
            IsCommandExpectedToFail = isCommandExpectedToFail;
            UniqueFor = ToUniqueForOptionFlags(uniqueForOptions);
        }

        /// <summary>
        /// Gets the name of locally installed template.
        /// </summary>
        public string TemplateName { get; init; }

        /// <summary>
        /// Gets the path to template.json file or containing directory.
        /// </summary>
        public string? TemplatePath { get; init; }

        /// <summary>
        /// Gets the template specific arguments.
        /// </summary>
        public IEnumerable<string> TemplateSpecificArgs { get; init; }

        /// <summary>
        /// Gets the directory with snapshot files.
        /// </summary>
        public string? SnapshotsDirectory { get; init; }

        /// <summary>
        /// Gets a custom prefix prepended in front of generated scenario name - result used for naming verification subdirectories.
        /// </summary>
        public string? ScenarioDistinguisher { get; init; }

        /// <summary>
        /// Gets the target directory to output the generated template.
        /// </summary>
        public string? OutputDirectory { get; init; }

        /// <summary>
        /// If set to true - the diff tool won't be automatically started by the Verifier on verification failures.
        /// </summary>
        public bool DisableDiffTool { get; init; }

        /// <summary>
        /// If set to true - all template output files will be verified, unless <see cref="VerificationExcludePatterns"/> are specified.
        /// Otherwise a default exclusions (to be documented - mostly binaries etc.).
        /// </summary>
        public bool DisableDefaultVerificationExcludePatterns { get; init; }

        /// <summary>
        /// Set of patterns defining files to be excluded from verification.
        /// </summary>
        public IEnumerable<string>? VerificationExcludePatterns { get; init; }

        /// <summary>
        /// Set of patterns defining files to be included into verification (unless excluded by <see cref="VerificationExcludePatterns"/>).
        /// By default all files are included (unless excluded).
        /// </summary>
        public IEnumerable<string>? VerificationIncludePatterns { get; init; }

        /// <summary>
        /// If set to true - 'dotnet new' command standard output and error contents will be verified along with the produced template files.
        /// </summary>
        public bool VerifyCommandOutput { get; init; }

        /// <summary>
        /// If set to true - 'dotnet new' command is expected to return nonzero return code.
        /// Otherwise a zero error code and no error output is expected.
        /// </summary>
        public bool IsCommandExpectedToFail { get; init; }

        /// <summary>
        /// Gets the Verifier expectations directory naming convention - by indicating which scenarios should be differentiated.
        /// </summary>
        public UniqueForOption? UniqueFor { get; init; }

        public static IEnumerable<string> TokenizeJoinedArgs(string? joinedArgs)
        {
            if (string.IsNullOrEmpty(joinedArgs))
            {
                return Enumerable.Empty<string>();
            }

            if (!joinedArgs.Contains('"') && !joinedArgs.Contains('\''))
            {
                return joinedArgs.Split().Where(s => !string.IsNullOrWhiteSpace(s));
            }

            return Regex.Matches(joinedArgs, @"[\""'].+?[\""']|[^ ]+")
                .Cast<Match>()
                .Select(m => m.Value)
                .Select(RemoveEnclosingQuotation)
                .Where(s => !string.IsNullOrWhiteSpace(s));
        }

        public static UniqueForOption? ToUniqueForOptionFlags(IEnumerable<UniqueForOption>? uniqueForOptions)
        {
            return uniqueForOptions?.Aggregate(UniqueForOption.None, (a, b) => a | b);
        }

        private static string RemoveEnclosingQuotation(string input)
        {
            int indexOfLast = input.Length - 1;

            if (
                indexOfLast >= 1 &&
                ((input[0] == '"' && input[indexOfLast] == '"') || (input[0] == '\'' && input[indexOfLast] == '\'')))
            {
                return input.Substring(1, indexOfLast - 1);
            }
            else
            {
                return input;
            }
        }
    }
}

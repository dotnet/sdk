// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Options;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier
{
    /// <summary>
    /// Configuration bag for the <see cref="VerificationEngine"/> class.
    /// </summary>
    public class TemplateVerifierOptions : IOptions<TemplateVerifierOptions>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateVerifierOptions"/> class.
        /// </summary>
        /// <param name="templateName"></param>
        public TemplateVerifierOptions(string templateName) => TemplateName = templateName;

        /// <summary>
        /// Gets the name of the template to be verified. Can be already installed template or a template within local path specified with <see cref="TemplatePath"/>.
        /// </summary>
        public string TemplateName { get; init; }

        /// <summary>
        /// Gets the path to template.json file or containing directory.
        /// </summary>
        public string? TemplatePath { get; init; }

        /// <summary>
        /// Gets the path to custom dotnet executable (e.g. x-copy install scenario).
        /// </summary>
        public string? DotnetExecutablePath { get; init; }

        /// <summary>
        /// Gets the custom environment variable collection to be passed to execution of dotnet commands.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Environment { get; private set; }

        /// <summary>
        /// Gets the template specific arguments.
        /// </summary>
        public IEnumerable<string>? TemplateSpecificArgs { get; init; }

        /// <summary>
        /// Gets the directory with snapshot files.
        /// </summary>
        public string? SnapshotsDirectory { get; init; }

        /// <summary>
        /// If set to true - 'dotnet new' command standard output and error contents will be verified along with the produced template files.
        /// </summary>
        public bool VerifyCommandOutput { get; init; }

        /// <summary>
        /// If set to true - 'dotnet new' command is expected to return nonzero return code.
        /// Otherwise a zero exit code and no error output is expected.
        /// </summary>
        public bool IsCommandExpectedToFail { get; init; }

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
        /// Gets a set of patterns defining files to be included into verification (unless excluded by <see cref="VerificationExcludePatterns"/>).
        /// By default all files are included (unless excluded).
        /// </summary>
        public IEnumerable<string>? VerificationIncludePatterns { get; init; }

        /// <summary>
        /// Gets the target directory to output the generated template.
        /// If explicitly specified, it won't be cleaned up upon successful run of test scenario.
        /// </summary>
        public string? OutputDirectory { get; init; }

        /// <summary>
        /// Gets the settings directory for template engine (in memory location used if not specified).
        /// </summary>
        public string? SettingsDirectory { get; init; }

        /// <summary>
        /// Gets the Verifier expectations directory naming convention - by indicating which scenarios should be differentiated.
        /// </summary>
        public UniqueForOption? UniqueFor { get; init; }

        /// <summary>
        /// Gets the delegates that perform custom scrubbing of template output contents before verifications.
        /// </summary>
        public ScrubbersDefinition? CustomScrubbers { get; private set; }

        /// <summary>
        /// Gets the delegate that performs custom verification of template output contents.
        /// </summary>
        public VerifyDirectory? CustomDirectoryVerifier { get; private set; }

        /// <summary>
        /// Gets the custom scenario name; if specified it will be used as part of verification snapshot.
        /// </summary>
        public string? ScenarioName { get; init; }

        /// <summary>
        /// <see langword="true" />, if the instantiation args should not be appended to verification snapshot name.
        /// </summary>
        public bool DoNotAppendTemplateArgsToScenarioName { get; init; }

        /// <summary>
        /// <see langword="true" />, if the template name should not be prepended to verification snapshot name.
        /// </summary>
        public bool DoNotPrependTemplateNameToScenarioName { get; init; }

        /// <summary>
        /// <see langword="true" />, if the caller method name should not be prepended to verification snapshot name.
        /// </summary>
        public bool DoNotPrependCallerMethodNameToScenarioName { get; init; }

        /// <summary>
        /// <see langword="true" />, if the output directory has to be empty before the test execution.
        /// </summary>
        public bool EnsureEmptyOutputDirectory { get; init; } = true;

        /// <summary>
        /// Gets the extension of autogeneratedfiles with stdout and stderr content.
        /// </summary>
        public string? StandardOutputFileExtension { get; init; }

        /// <summary>
        /// Gets the delegate that performs custom generation of template output contents.
        /// </summary>
        public RunInstantiation? CustomInstatiation { get; private set; }

        // Enable if too many underlying features are asked.
        //  On the other hand if possible, we should wrap everyting and dfo not expose underlying technology to allow for change.
        // public Action<VerifySettings> VerifySettingsAdjustor { get; init; }

        TemplateVerifierOptions IOptions<TemplateVerifierOptions>.Value => this;

        /// <summary>
        /// Adds environment variables collection to be passed to execution of dotnet commands.
        /// </summary>
        /// <param name="environment"></param>
        /// <returns></returns>
        public TemplateVerifierOptions WithCustomEnvironment(IReadOnlyDictionary<string, string> environment)
        {
            Dictionary<string, string> newEnvironemnt = new(environment);
            if (Environment != null)
            {
                newEnvironemnt.Merge(Environment);
            }
            Environment = newEnvironemnt;
            return this;
        }

        /// <summary>
        /// Adds environment variable to be passed to execution of dotnet commands.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public TemplateVerifierOptions WithEnvironmentVariable(string name, string value)
        {
            return WithCustomEnvironment(new Dictionary<string, string>() { { name, value } });
        }

        /// <summary>
        /// Adds a custom scrubber definition.
        /// The scrubber definition can alter the template content (globally or based on the file extension), before the verifications occur.
        /// </summary>
        /// <param name="scrubbers"></param>
        /// <returns></returns>
        public TemplateVerifierOptions WithCustomScrubbers(ScrubbersDefinition scrubbers)
        {
            CustomScrubbers = scrubbers;
            return this;
        }

        /// <summary>
        /// Adds on optional custom verifier implementation.
        /// If custom verifier is provided, no default verifications of content will be performed - the caller is responsible for performing the verifications.
        /// </summary>
        /// <param name="verifier"></param>
        /// <returns></returns>
        public TemplateVerifierOptions WithCustomDirectoryVerifier(VerifyDirectory verifier)
        {
            CustomDirectoryVerifier = verifier;
            return this;
        }

        /// <summary>
        /// Adds custom template instantiator.
        /// If custom template generator is provided it's responsible for proper instantiation of a template (as well as installation if needed) based on given options.
        /// </summary>
        /// <param name="instantiation"></param>
        /// <returns></returns>
        public TemplateVerifierOptions WithCustomInstatiation(RunInstantiation instantiation)
        {
            CustomInstatiation = instantiation;
            return this;
        }
    }
}

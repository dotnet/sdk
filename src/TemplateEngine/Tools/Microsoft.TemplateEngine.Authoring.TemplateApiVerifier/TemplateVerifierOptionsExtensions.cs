// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.IDE;
using WellKnownSearchFilters = Microsoft.TemplateEngine.Utils.WellKnownSearchFilters;

namespace Microsoft.TemplateEngine.Authoring.TemplateApiVerifier
{
    public static class TemplateVerifierOptionsExtensions
    {
        /// <summary>
        /// Adds custom template instantiator that runs template instantiation in-proc via TemplateCreator API.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="inputParameters"></param>
        /// <returns></returns>
        public static TemplateVerifierOptions WithInstantiationThroughTemplateCreatorApi(
            this TemplateVerifierOptions options,
            IReadOnlyDictionary<string, string?>? inputParameters)
        {
            return options.WithCustomInstatiation(async verifierOptions => await RunInstantiation(verifierOptions, inputParameters, null).ConfigureAwait(false));
        }

        /// <summary>
        /// Adds custom template instantiator that runs template instantiation in-proc via TemplateCreator API.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="inputDataSet"></param>
        /// <returns></returns>
        public static TemplateVerifierOptions WithInstantiationThroughTemplateCreatorApi(
            this TemplateVerifierOptions options,
            InputDataSet? inputDataSet)
        {
            return options.WithCustomInstatiation(async verifierOptions => await RunInstantiation(verifierOptions, null, inputDataSet).ConfigureAwait(false));
        }

        private static async Task<IInstantiationResult> RunInstantiation(
            TemplateVerifierOptions options,
            IReadOnlyDictionary<string, string?>? inputParameters,
            InputDataSet? inputDataSet)
        {
            if (!string.IsNullOrEmpty(options.DotnetExecutablePath))
            {
                throw new TemplateVerificationException(LocalizableStrings.Error_DotnetPath, TemplateVerificationErrorCode.InvalidOption);
            }

            if (options.TemplateSpecificArgs != null)
            {
                throw new TemplateVerificationException(LocalizableStrings.Error_TemplateArgsDisalowed, TemplateVerificationErrorCode.InvalidOption);
            }

            // Create temp folder and instantiate there
            string workingDir = options.OutputDirectory ?? Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (Directory.Exists(workingDir) && Directory.EnumerateFileSystemEntries(workingDir).Any())
            {
                throw new TemplateVerificationException(TemplateVerifier.LocalizableStrings.VerificationEngine_Error_WorkDirExists, TemplateVerificationErrorCode.WorkingDirectoryExists);
            }

            if (!ExtractParameter(inputParameters, inputDataSet, "name", out string? name) &&
                !ExtractParameter(inputParameters, inputDataSet, "n", out name))
            {
                name = options.TemplateName;
            }

            if (!ExtractParameter(inputParameters, inputDataSet, "output", out string? output) &&
                !ExtractParameter(inputParameters, inputDataSet, "o", out output))
            {
                output = options.TemplateName;
            }

            string outputPath = Path.Combine(workingDir, output!);

            var host = new DefaultTemplateEngineHost(
                nameof(TemplateVerifierOptionsExtensions),
                "1.0.0",
                null,
                new List<(Type, IIdentifiedComponent)>(),
                Array.Empty<string>());

            var bootstrapper = new Bootstrapper(
                host,
                virtualizeConfiguration: string.IsNullOrEmpty(options.SettingsDirectory),
                loadDefaultComponents: true,
                hostSettingsLocation: options.SettingsDirectory,
                environment: new VirtualEnvironment(options.Environment, true));

            if (!string.IsNullOrEmpty(options.TemplatePath))
            {
                await InstallTemplateAsync(bootstrapper, options.TemplatePath).ConfigureAwait(false);
            }

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter(options.TemplateName) }).ConfigureAwait(false);

            if (foundTemplates.Count == 0)
            {
                throw new TemplateVerificationException(LocalizableStrings.Error_NoPackages, TemplateVerificationErrorCode.InstallFailed);
            }

            ITemplateInfo template = foundTemplates[0].Info;

            ITemplateCreationResult result = await
                (inputDataSet != null
                    ? bootstrapper.CreateAsync(
                        info: template,
                        name: name,
                        inputParameters: inputDataSet,
                        outputPath: outputPath)
                    : bootstrapper.CreateAsync(
                        info: template,
                        name: name,
                        parameters: inputParameters ?? new Dictionary<string, string?>(),
                        outputPath: outputPath)).ConfigureAwait(false);

            return new CommandResultData(
                (int)result.Status,
                result.Status == CreationResultStatus.Success ? string.Format(LocalizableStrings.CreateSuccessful, result.TemplateFullName) : string.Empty,
                result.ErrorMessage ?? string.Empty,
                // We do not want to use result.OutputBaseDirectory as it points to the base of template
                //  not a working dir of command (which is one level up - as we explicitly specify output subdir, as if '-o' was passed)
                workingDir);
        }

        private static async Task InstallTemplateAsync(Bootstrapper bootstrapper, string template)
        {
            List<InstallRequest> installRequests = new List<InstallRequest>() { new InstallRequest(Path.GetFullPath(template)) };

            IReadOnlyList<InstallResult> installationResults = await bootstrapper.InstallTemplatePackagesAsync(installRequests).ConfigureAwait(false);
            InstallResult? failedResult = installationResults.FirstOrDefault(result => !result.Success);
            if (failedResult != null)
            {
                throw new TemplateVerificationException(string.Format("Failed to install template: {0}, details:{1}", failedResult.InstallRequest.PackageIdentifier, failedResult.ErrorMessage), TemplateVerificationErrorCode.InstallFailed);
            }
        }

        private static bool ExtractParameter(
            IReadOnlyDictionary<string, string?>? inputParameters,
            InputDataSet? inputDataSet,
            string key,
            out string? value)
        {
            value = null;
            if (
                inputDataSet != null &&
                inputDataSet.ParameterDefinitionSet.TryGetValue(key, out ITemplateParameter parameter) &&
                inputDataSet.TryGetValue(parameter, out InputParameterData parameterValue) &&
                parameterValue.Value != null)
            {
                value = parameterValue.Value.ToString();
            }
            else
            {
                inputParameters?.TryGetValue(key, out value);
            }

            return !string.IsNullOrEmpty(value);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Template
{
    /// <summary>
    /// The class instantiates and dry run given template with given parameters.
    /// </summary>
    public class TemplateCreator
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly ILogger _logger;

        public TemplateCreator(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _logger = _environmentSettings.Host.LoggerFactory.CreateLogger<TemplateCreator>();
        }

        /// <summary>
        /// Instantiates or dry runs the template.
        /// </summary>
        /// <param name="templateInfo">The template to run.</param>
        /// <param name="name">The name to use. Will be also used as <paramref name="outputPath"/> in case it is empty and <see cref="ITemplate.IsNameAgreementWithFolderPreferred"/> for <paramref name="templateInfo"/> is set to true.</param>
        /// <param name="fallbackName">Fallback name in case <paramref name="name"/> is null.</param>
        /// <param name="outputPath">The output directory for instantiate template to.</param>
        /// <param name="inputParameters">The input parameters for the template.</param>
        /// <param name="forceCreation">If true, create the template even it overwrites existing files.</param>
        /// <param name="baselineName">baseline configuration to use.</param>
        /// <param name="dryRun">If true, only dry run will be performed - no actual actions will be done.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
        public Task<ITemplateCreationResult> InstantiateAsync(
            ITemplateInfo templateInfo,
            string? name,
            string? fallbackName,
            string? outputPath,
            IReadOnlyDictionary<string, string?> inputParameters,
            bool forceCreation = false,
            string? baselineName = null,
            bool dryRun = false,
            CancellationToken cancellationToken = default)
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
        {
            return InstantiateAsync(
                templateInfo,
                name,
                fallbackName,
                outputPath,
                new InputDataSet(templateInfo, inputParameters),
                forceCreation,
                baselineName,
                dryRun,
                cancellationToken);
        }

        /// <summary>
        /// Instantiates or dry runs the template.
        /// </summary>
        /// <param name="templateInfo">The template to run.</param>
        /// <param name="name">The name to use. Will be also used as <paramref name="outputPath"/> in case it is empty and <see cref="ITemplate.IsNameAgreementWithFolderPreferred"/> for <paramref name="templateInfo"/> is set to true.</param>
        /// <param name="fallbackName">Fallback name in case <paramref name="name"/> is null.</param>
        /// <param name="outputPath">The output directory for instantiate template to.</param>
        /// <param name="inputParameters">The input parameters for the template.</param>
        /// <param name="forceCreation">If true, create the template even it overwrites existing files.</param>
        /// <param name="baselineName">baseline configuration to use.</param>
        /// <param name="dryRun">If true, only dry run will be performed - no actual actions will be done.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
        public async Task<ITemplateCreationResult> InstantiateAsync(
            ITemplateInfo templateInfo,
            string? name,
            string? fallbackName,
            string? outputPath,
            InputDataSet? inputParameters,
            bool forceCreation = false,
            string? baselineName = null,
            bool dryRun = false,
            CancellationToken cancellationToken = default)
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
        {
            _ = templateInfo ?? throw new ArgumentNullException(nameof(templateInfo));
            inputParameters?.VerifyInputData();
            inputParameters ??= new InputDataSet(templateInfo);
            cancellationToken.ThrowIfCancellationRequested();

            ITemplate? template = LoadTemplate(templateInfo, baselineName);
            if (template == null)
            {
                return new TemplateCreationResult(CreationResultStatus.NotFound, templateInfo.Name, LocalizableStrings.TemplateCreator_TemplateCreationResult_Error_CouldNotLoadTemplate);
            }

            string? realName = name ?? fallbackName ?? template.DefaultName;
            if (string.IsNullOrWhiteSpace(realName))
            {
                return new TemplateCreationResult(CreationResultStatus.MissingMandatoryParam, template.Name, "--name");
            }
            if (template.IsNameAgreementWithFolderPreferred && string.IsNullOrEmpty(outputPath))
            {
                outputPath = name;
            }
            string targetDir = !string.IsNullOrWhiteSpace(outputPath) ? outputPath! : _environmentSettings.Host.FileSystem.GetCurrentDirectory();
            Timing contentGeneratorBlock = Timing.Over(_logger, "Template content generation");
            try
            {
                ICreationResult? creationResult = null;
                if (!dryRun)
                {
                    _environmentSettings.Host.FileSystem.CreateDirectory(targetDir);
                }
                IComponentManager componentManager = _environmentSettings.Components;

                // setup separate sets of parameters to be used for GetCreationEffects() and by CreateAsync().
                if (!TryCreateParameterSet(template, realName!, inputParameters, out IParameterSetData? effectParams, out TemplateCreationResult? resultIfParameterCreationFailed))
                {
                    //resultIfParameterCreationFailed is not null when TryCreateParameterSet is false
                    return resultIfParameterCreationFailed!;
                }
                if (effectParams is null)
                {
                    throw new InvalidOperationException($"{nameof(effectParams)} cannot be null when {nameof(TryCreateParameterSet)} returns 'true'");
                }

                ICreationEffects creationEffects = await template.Generator.GetCreationEffectsAsync(
                    _environmentSettings,
                    template,
                    effectParams,
                    targetDir,
                    cancellationToken).ConfigureAwait(false);
                IReadOnlyList<IFileChange> changes = creationEffects.FileChanges;
                IReadOnlyList<IFileChange> destructiveChanges = changes.Where(x => x.ChangeKind != ChangeKind.Create).ToList();

                if (!forceCreation && destructiveChanges.Count > 0)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    if (!_environmentSettings.Host.OnPotentiallyDestructiveChangesDetected(changes, destructiveChanges))
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        return new TemplateCreationResult(
                            CreationResultStatus.DestructiveChangesDetected,
                            template.Name,
                            LocalizableStrings.TemplateCreator_TemplateCreationResult_Error_DestructiveChanges,
                            null,
                            null,
                            creationEffects);
                    }
                }

                if (!TryCreateParameterSet(template, realName!, inputParameters, out IParameterSetData? creationParams, out resultIfParameterCreationFailed))
                {
                    return resultIfParameterCreationFailed!;
                }

                if (creationParams is null)
                {
                    throw new InvalidOperationException($"{nameof(creationParams)} cannot be null when {nameof(TryCreateParameterSet)} returns 'true'");
                }

                if (!dryRun)
                {
                    creationResult = await template.Generator.CreateAsync(
                        _environmentSettings,
                        template,
                        creationParams,
                        targetDir,
                        cancellationToken).ConfigureAwait(false);
                }
                return new TemplateCreationResult(
                    status: CreationResultStatus.Success,
                    templateName: template.Name,
                    creationOutputs: creationResult,
                    outputBaseDir: targetDir,
                    creationEffects: creationEffects);
            }
            catch (Exception cx)
            {
                string message = string.Join(Environment.NewLine, ExceptionMessages(cx));
                return new TemplateCreationResult(
                    status: cx is TemplateAuthoringException ? CreationResultStatus.TemplateIssueDetected : CreationResultStatus.CreateFailed,
                    templateName: template.Name,
                    localizedErrorMessage: string.Format(LocalizableStrings.TemplateCreator_TemplateCreationResult_Error_CreationFailed, message),
                    outputBaseDir: targetDir);
            }
            finally
            {
#pragma warning disable CS0618 // Type or member is obsolete - temporary until the method becomes internal.
                ReleaseMountPoints(template);
#pragma warning restore CS0618 // Type or member is obsolete
                contentGeneratorBlock.Dispose();
            }
        }

        [Obsolete("The method is deprecated.")]
        //This method should become internal once Cli help logic is refactored.
        public void ReleaseMountPoints(ITemplate template)
        {
            if (template == null)
            {
                return;
            }

            template.LocaleConfiguration?.MountPoint.Dispose();

            template.Configuration?.MountPoint.Dispose();

            if (template.TemplateSourceRoot != null && template.TemplateSourceRoot != template.Configuration)
            {
                template.TemplateSourceRoot.MountPoint.Dispose();
            }
        }

        /// <summary>
        /// Reads the parameters from the template and the host and setup their values in the return IParameterSet.
        /// Host param values override template defaults.
        /// </summary>
        /// <param name="templateInfo"></param>
        /// <param name="realName"></param>
        /// <param name="paramsWithInvalidValues"></param>
        /// <returns></returns>
        [Obsolete("This method is deprecated.")]
        //This method should become internal once Cli help logic is refactored.
        public IParameterSet SetupDefaultParamValuesFromTemplateAndHost(ITemplate templateInfo, string realName, out IReadOnlyList<string> paramsWithInvalidValues)
        {
            return (IParameterSet)SetupDefaultParamValuesFromTemplateAndHostInternal(templateInfo, realName, out paramsWithInvalidValues);
        }

        /// <summary>
        /// The template params for which there are same-named input parameters have their values set to the corresponding input parameters value.
        /// input parameters that do not have corresponding template params are ignored.
        /// </summary>
        /// <param name="template"></param>
        /// <param name="templateParams"></param>
        /// <param name="inputParameters"></param>
        /// <param name="paramsWithInvalidValues"></param>
        [Obsolete("This method is deprecated.")]
        //This method should become internal once Cli help logic is refactored.
        public void ResolveUserParameters(ITemplate template, IParameterSet templateParams, IReadOnlyDictionary<string, string?> inputParameters, out IReadOnlyList<string> paramsWithInvalidValues)
        {
            ResolveUserParameters(template, new LegacyParamSetWrapper(templateParams), templateParams.ToInputDataSet(), out paramsWithInvalidValues);
        }

        private bool AnyParametersWithInvalidDefaultsUnresolved(IReadOnlyList<string> defaultParamsWithInvalidValues, InputDataSet inputParameters, out IReadOnlyList<string> invalidDefaultParameters)
        {
            invalidDefaultParameters = defaultParamsWithInvalidValues.Where(x => !inputParameters.ParameterDefinitionSet.ContainsKey(x)).ToList();
            return invalidDefaultParameters.Count > 0;
        }

        private IParameterSetBuilder SetupDefaultParamValuesFromTemplateAndHostInternal(ITemplate template, string realName, out IReadOnlyList<string> paramsWithInvalidValues)
        {
            return ParameterSetBuilder.CreateWithDefaults(template.Generator, template.ParameterDefinitions, realName, _environmentSettings, out paramsWithInvalidValues);
        }

        private void ResolveUserParameters(ITemplate template, IParameterSetBuilder templateParamsBuilder, InputDataSet inputParameters, out IReadOnlyList<string> paramsWithInvalidValues)
        {
            List<string> tmpParamsWithInvalidValues = new List<string>();
            paramsWithInvalidValues = tmpParamsWithInvalidValues;

            foreach (InputParameterData inputParam in inputParameters
                         .Where(p => p.Value.InputDataState != InputDataState.Unset &&
                                     !(p.Value is EvaluatedInputParameterData
                                     {
                                         IsEnabledConditionResult: false
                                     }))
                         .Select(p => p.Value))
            {
                if (templateParamsBuilder.TryGetValue(inputParam.ParameterDefinition.Name, out ITemplateParameter paramFromTemplate))
                {
                    if (inputParam.Value == null)
                    {
                        if (!string.IsNullOrEmpty(paramFromTemplate.DefaultIfOptionWithoutValue))
                        {
                            object? resolvedValue = template.Generator.ConvertParameterValueToType(_environmentSettings, paramFromTemplate, paramFromTemplate.DefaultIfOptionWithoutValue!, out bool valueResolutionError);
                            if (!valueResolutionError)
                            {
                                if (resolvedValue is null)
                                {
                                    throw new InvalidOperationException($"{nameof(resolvedValue)} cannot be null when {nameof(valueResolutionError)} is 'false'.");
                                }
                                templateParamsBuilder.SetParameterValue(paramFromTemplate, resolvedValue, DataSource.DefaultIfNoValue);
                            }
                            // don't fail on value resolution errors, but report them as authoring problems.
                            else
                            {
                                _logger.LogDebug($"Template {template.Identity} has an invalid DefaultIfOptionWithoutValue value for parameter {inputParam.ParameterDefinition.Name}"); // CodeQL [cs/privacy/suspicious-logging-arguments] False Positive: CodeQL wrongly detected "Identity"
                            }
                        }
                        else
                        {
                            tmpParamsWithInvalidValues.Add(paramFromTemplate.Name);
                        }
                    }
                    else
                    {
                        object? resolvedValue = template.Generator.ConvertParameterValueToType(_environmentSettings, paramFromTemplate, inputParam.Value.ToString(), out bool valueResolutionError);
                        if (!valueResolutionError)
                        {
                            if (resolvedValue is null)
                            {
                                throw new InvalidOperationException($"{nameof(resolvedValue)} cannot be null when {nameof(valueResolutionError)} is 'false'.");
                            }
                            templateParamsBuilder.SetParameterValue(paramFromTemplate, resolvedValue, DataSource.User);
                        }
                        else
                        {
                            tmpParamsWithInvalidValues.Add(paramFromTemplate.Name);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fully load template from <see cref="ITemplateInfo"/>.
        /// <see cref="ITemplateInfo"/> usually comes from cache and is missing some information.
        /// Calling this methods returns full information about template needed to instantiate template.
        /// </summary>
        /// <param name="info">Information about template.</param>
        /// <param name="baselineName">Defines which baseline of template to load.</param>
        /// <returns>Fully loaded template or <c>null</c> if it fails to load template.</returns>
#pragma warning disable SA1202 // Elements should be ordered by access
        public ITemplate? LoadTemplate(ITemplateInfo info, string? baselineName)
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            if (!_environmentSettings.Components.TryGetComponent(info.GeneratorId, out IGenerator? generator))
            {
                return null;
            }
            if (!_environmentSettings.TryGetMountPoint(info.MountPointUri, out IMountPoint? mountPoint))
            {
                return null;
            }
            IFile? config = mountPoint!.FileInfo(info.ConfigPlace);
            if (config == null)
            {
                return null;
            }
            IFile? localeConfig = string.IsNullOrEmpty(info.LocaleConfigPlace) ? null : mountPoint.FileInfo(info.LocaleConfigPlace!);
            IFile? hostTemplateConfigFile = string.IsNullOrEmpty(info.HostConfigPlace) ? null : mountPoint.FileInfo(info.HostConfigPlace!);
            using (Timing.Over(_environmentSettings.Host.Logger, $"Template from config {config.MountPoint.MountPointUri}{config.FullPath}"))
            {
                if (generator!.TryGetTemplateFromConfigInfo(config, out ITemplate? template, localeConfig, hostTemplateConfigFile, baselineName))
                {
                    return template;
                }
                else
                {
                    //TODO: Log the failure to read the template info
                }
            }

            return null;
        }

        private static IEnumerable<string> ExceptionMessages(Exception? e)
        {
            while (e != null)
            {
                yield return e.Message;
                e = e.InnerException;
            }
        }

        /// <summary>
        /// Checks that all required parameters are provided. If a missing one is found, a value may be provided via host.OnParameterError
        /// but it's up to the caller / UI to decide how to act.
        /// Returns true if there are any missing params, false otherwise.
        /// </summary>
        /// <param name="templateParams"></param>
        /// <param name="parametersBuilder"></param>
        /// <param name="missingParamNames"></param>
        /// <returns></returns>
        private bool CheckForMissingRequiredParameters(InputDataSet templateParams, IParameterSetBuilder parametersBuilder, out IList<string> missingParamNames)
        {
            ITemplateEngineHost host = _environmentSettings.Host;
            bool anyMissingParams = false;
            missingParamNames = new List<string>();

            foreach (InputParameterData evaluatedParameterData in templateParams.Values.Where(v => v.GetEvaluatedPrecedence() == EvaluatedPrecedence.Required && v.InputDataState == InputDataState.Unset))
            {
                string? newParamValue;
                const int _MAX_RETRIES = 3;
                int retries = 0;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                while (host.OnParameterError(evaluatedParameterData.ParameterDefinition, string.Empty, "Missing required parameter", out newParamValue)
#pragma warning restore CS0618 // Type or member is obsolete
                       && string.IsNullOrEmpty(newParamValue)
                       && ++retries < _MAX_RETRIES)
                {
                }

                if (!string.IsNullOrEmpty(newParamValue))
                {
                    parametersBuilder.SetParameterValue(evaluatedParameterData.ParameterDefinition, newParamValue!, DataSource.HostOnError);
                }
                else
                {
                    missingParamNames.Add(evaluatedParameterData.ParameterDefinition.Name);
                    anyMissingParams = true;
                }
            }

            return anyMissingParams;
        }

        private InputDataSet? EvaluateConditionalParameters(
            IParameterSetBuilder parametersBuilder,
            InputDataSet inputParameters,
            ITemplate template,
            out IReadOnlyList<string> paramsWithInvalidValues,
            out bool isExternalEvaluationInvalid)
        {
            if (!inputParameters.HasConditions())
            {
                paramsWithInvalidValues = Array.Empty<string>();
                isExternalEvaluationInvalid = false;
                return parametersBuilder.Build(false, template.Generator, _logger);
            }

            bool isEvaluatedExternally = false;
            foreach (EvaluatedInputParameterData evaluatedParameterData in inputParameters.Values.OfType<EvaluatedInputParameterData>())
            {
                isEvaluatedExternally = true;
                parametersBuilder.SetParameterEvaluation(evaluatedParameterData.ParameterDefinition, evaluatedParameterData);
            }

            if (isEvaluatedExternally)
            {
                if (!parametersBuilder.CheckIsParametersEvaluationCorrect(template.Generator, _logger, out paramsWithInvalidValues))
                {
                    _logger.LogInformation(
                        "Parameters conditions ('IsEnbaled', 'IsRequired') evaluation supplied by host didn't match validation against internal evaluation for following parameters: [{0}]. Host requested to continue in such case: {1}",
                        paramsWithInvalidValues.ToCsvString(),
                        inputParameters.ContinueOnMismatchedConditionsEvaluation);

                    if (!inputParameters.ContinueOnMismatchedConditionsEvaluation)
                    {
                        isExternalEvaluationInvalid = true;
                        return null;
                    }
                }
            }

            isExternalEvaluationInvalid = false;
            InputDataSet evaluatedParameterSetData = parametersBuilder.Build(!isEvaluatedExternally, template.Generator, _logger);
            List<string> defaultParamsWithInvalidValues = new List<string>();

            // for params that changed to optional and do not have values - try get 'Default' value (as for required it's not obtained)
            evaluatedParameterSetData.Values
                .Where(v =>
                    v.InputDataState != InputDataState.Set &&
                    !string.IsNullOrEmpty(v.ParameterDefinition.Precedence.IsRequiredCondition) &&
                    v is EvaluatedInputParameterData evaluated &&
                    evaluated.GetEvaluatedPrecedence() != EvaluatedPrecedence.Disabled &&
                    evaluated.GetEvaluatedPrecedence() != EvaluatedPrecedence.Required)
                .ForEach(p => parametersBuilder.SetParameterDefault(template.Generator, p.ParameterDefinition, _environmentSettings, false, false, defaultParamsWithInvalidValues));

            paramsWithInvalidValues = defaultParamsWithInvalidValues;
            return evaluatedParameterSetData;
        }

        private bool TryCreateParameterSet(ITemplate template, string realName, InputDataSet inputParameters, out IParameterSetData? templateParams, out TemplateCreationResult? failureResult)
        {
            // there should never be param errors here. If there are, the template is malformed, or the host gave an invalid value.
#pragma warning disable CS0618 // Type or member is obsolete - temporary until the method becomes internal.
            IParameterSetBuilder parameterSetBuilder = SetupDefaultParamValuesFromTemplateAndHostInternal(template, realName, out IReadOnlyList<string> defaultParamsWithInvalidValues);

            ResolveUserParameters(template, parameterSetBuilder, inputParameters, out IReadOnlyList<string> userParamsWithInvalidValues);

            if (AnyParametersWithInvalidDefaultsUnresolved(defaultParamsWithInvalidValues, inputParameters, out IReadOnlyList<string> defaultsWithUnresolvedInvalidValues)
#pragma warning restore CS0618 // Type or member is obsolete
                    || userParamsWithInvalidValues.Count > 0)
            {
                string message = string.Join(", ", new CombinedList<string>(userParamsWithInvalidValues, defaultsWithUnresolvedInvalidValues));
                failureResult = new TemplateCreationResult(CreationResultStatus.InvalidParamValues, template.Name, message);
                templateParams = null;
                return false;
            }

            InputDataSet? evaluatedParams = EvaluateConditionalParameters(parameterSetBuilder, inputParameters, template, out var paramsWithInvalidValues, out bool isExternalEvaluationInvalid);
            if (paramsWithInvalidValues.Any())
            {
                failureResult = new TemplateCreationResult(isExternalEvaluationInvalid ? CreationResultStatus.CondtionsEvaluationMismatch : CreationResultStatus.InvalidParamValues, template.Name, paramsWithInvalidValues.ToCsvString());
                templateParams = null;
                return false;
            }

            bool missingParams = CheckForMissingRequiredParameters(evaluatedParams!, parameterSetBuilder, out IList<string> missingParamNames);

            if (missingParams)
            {
                failureResult = new TemplateCreationResult(CreationResultStatus.MissingMandatoryParam, template.Name, string.Join(", ", missingParamNames));
                templateParams = null;
                return false;
            }

            templateParams = parameterSetBuilder.Build(false, template.Generator, _logger).ToParameterSetData();

            failureResult = null;
            return true;
        }

        /// <summary>
        /// This class is to be used solely to hold backward compatibility of <see cref="ResolveUserParameters"/> method.
        ///  Hence only the base and <see cref="SetParameterValue"/> are implemented - no other methods are called in scope of <see cref="ResolveUserParameters"/>.
        /// </summary>
        [Obsolete("Proxy for obsolete ResolveUserParameters method", false)]
        private class LegacyParamSetWrapper : ParameterDefinitionSet, IParameterSetBuilder
        {
            private readonly IParameterSet _parameterSet;

            public LegacyParamSetWrapper(IParameterSet parameterSet)
                : base(parameterSet.ParameterDefinitions) => _parameterSet = parameterSet;

            public bool CheckIsParametersEvaluationCorrect(IGenerator generator, ILogger logger, out IReadOnlyList<string> paramsWithInvalidEvaluations) =>
                throw new NotImplementedException();

            public InputDataSet Build(bool evaluateConditions, IGenerator generator, ILogger logger) => throw new NotImplementedException();

            public void SetParameterDefault(
                IGenerator generator,
                ITemplateParameter parameter,
                IEngineEnvironmentSettings environment,
                bool useHostDefaults,
                bool isRequired,
                List<string> paramsWithInvalidValues) =>
                throw new NotImplementedException();

            public bool HasParameterValue(ITemplateParameter parameter) => throw new NotImplementedException();

            public void SetParameterEvaluation(ITemplateParameter parameter, EvaluatedInputParameterData evaluatedParameterData) => throw new NotImplementedException();

            public void SetParameterValue(ITemplateParameter parameter, object value, DataSource dataSource) => _parameterSet.ResolvedValues[parameter] = value;
        }
    }
}

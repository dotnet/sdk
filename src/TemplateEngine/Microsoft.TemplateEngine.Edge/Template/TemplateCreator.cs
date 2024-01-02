// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
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
        {
            _ = templateInfo ?? throw new ArgumentNullException(nameof(templateInfo));
            inputParameters?.VerifyInputData();
            inputParameters ??= new InputDataSet(templateInfo);
            cancellationToken.ThrowIfCancellationRequested();

            using ITemplate? template = await LoadTemplateAsync(templateInfo, baselineName, cancellationToken).ConfigureAwait(false);

            if (template == null)
            {
                return new TemplateCreationResult(CreationResultStatus.NotFound, templateInfo.Name, LocalizableStrings.TemplateCreator_TemplateCreationResult_Error_CouldNotLoadTemplate);
            }

            ValidationUtils.LogValidationResults(_environmentSettings.Host.Logger, new[] { template });

            if (!template.IsValid)
            {
                return new TemplateCreationResult(
                        CreationResultStatus.TemplateIssueDetected, template.Name, LocalizableStrings.TemplateCreator_TemplateCreationResult_Error_InvalidTemplate);
            }

            string? realName = null;
            if (!string.IsNullOrEmpty(name))
            {
                realName = name;
            }
            else if (templateInfo.PreferDefaultName)
            {
                if (string.IsNullOrEmpty(templateInfo.DefaultName))
                {
                    return new TemplateCreationResult(
                        CreationResultStatus.TemplateIssueDetected, template.Name, LocalizableStrings.TemplateCreator_TemplateCreationResult_Error_NoDefaultName);
                }
                realName = templateInfo.DefaultName;
            }
            else
            {
                if (string.IsNullOrEmpty(fallbackName))
                {
                    if (!string.IsNullOrEmpty(templateInfo.DefaultName))
                    {
                        realName = templateInfo.DefaultName;
                    }
                }
                else
                {
                    realName = fallbackName;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

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
                contentGeneratorBlock.Dispose();
            }
        }

        /// <summary>
        /// Fully load template from <see cref="ITemplateInfo"/>.
        /// <see cref="ITemplateInfo"/> usually comes from cache and is missing some information.
        /// Calling this methods returns full information about template needed to instantiate template.
        /// </summary>
        /// <param name="info">Information about template.</param>
        /// <param name="baselineName">Defines which baseline of template to load.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Fully loaded template or <c>null</c> if it fails to load template.</returns>
        internal Task<ITemplate?> LoadTemplateAsync(ITemplateInfo info, string? baselineName, CancellationToken cancellationToken)
        {
            if (!_environmentSettings.Components.TryGetComponent(info.GeneratorId, out IGenerator? generator))
            {
                return Task.FromResult((ITemplate?)null);
            }
            using (Timing.Over(_environmentSettings.Host.Logger, $"Template from config {info.MountPointUri}{info.ConfigPlace}"))
            {
                return generator!.LoadTemplateAsync(_environmentSettings, ToITemplateLocator(info), baselineName, cancellationToken);
            }
        }

        private static IEnumerable<string> ExceptionMessages(Exception? e)
        {
            while (e != null)
            {
                yield return e.Message;
                e = e.InnerException;
            }
        }

        private static IExtendedTemplateLocator ToITemplateLocator(ITemplateInfo templateInfo)
        {
            return new TemplateLocator(templateInfo.GeneratorId, templateInfo.MountPointUri, templateInfo.ConfigPlace)
            {
                LocaleConfigPlace = templateInfo.LocaleConfigPlace,
                HostConfigPlace = templateInfo.HostConfigPlace,
            };
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
                            if (string.IsNullOrEmpty(paramFromTemplate.Precedence.IsEnabledCondition))
                            {
                                tmpParamsWithInvalidValues.Add(paramFromTemplate.Name);
                            }
                            // in case the IsEnabledCondition is configured - we'll check if parameter needed to be resolved
                            //   after we evaluate it's enablement condition (and for disabled parameter we do not care)
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
                if (!parametersBuilder.CheckIsParametersEvaluationCorrect(
                        template.Generator, _logger, !inputParameters.ContinueOnMismatchedConditionsEvaluation, out paramsWithInvalidValues))
                {
                    _logger.LogInformation(
                        "Parameters conditions ('IsEnabled', 'IsRequired') evaluation supplied by host didn't match validation against internal evaluation for following parameters: [{0}]. Host requested to continue in such case: {1}",
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

            IEnumerable<string> missingParams = evaluatedParams!.Values
                .Where(v => v.GetEvaluatedPrecedence() == EvaluatedPrecedence.Required && v.InputDataState == InputDataState.Unset)
                .Select(v => v.ParameterDefinition.Name);

            if (missingParams.Any())
            {
                failureResult = new TemplateCreationResult(CreationResultStatus.MissingMandatoryParam, template.Name, string.Join(", ", missingParams));
                templateParams = null;
                return false;
            }

            templateParams = parameterSetBuilder.Build(false, template.Generator, _logger).ToParameterSetData();

            failureResult = null;
            return true;
        }

        #region Obsolete members

        /// <summary>
        /// Fully load template from <see cref="ITemplateInfo"/>.
        /// <see cref="ITemplateInfo"/> usually comes from cache and is missing some information.
        /// Calling this methods returns full information about template needed to instantiate template.
        /// </summary>
        /// <param name="info">Information about template.</param>
        /// <param name="baselineName">Defines which baseline of template to load.</param>
        /// <returns>Fully loaded template or <c>null</c> if it fails to load template.</returns>
#pragma warning disable SA1202 // Elements should be ordered by access
        [Obsolete("The method is obsolete and won't be replaced. Use InstantiateAsync to run the template.")]
        public ITemplate? LoadTemplate(ITemplateInfo info, string? baselineName)
#pragma warning restore SA1202 // Elements should be ordered by access
        {
            return Task.Run(async () => await LoadTemplateAsync(info, baselineName, default).ConfigureAwait(false)).GetAwaiter().GetResult();
        }

        [Obsolete("The method is deprecated.")]
        //This method should become internal once Cli help logic is refactored.
        public void ReleaseMountPoints(ITemplate template)
        {
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

            public bool CheckIsParametersEvaluationCorrect(IGenerator generator, ILogger logger, bool throwOnError, out IReadOnlyList<string> paramsWithInvalidEvaluations) =>
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
        #endregion

        private class TemplateLocator : IExtendedTemplateLocator
        {
            public TemplateLocator(Guid generatorId, string mountPointUri, string configPlace)
            {
                GeneratorId = generatorId;
                MountPointUri = mountPointUri;
                ConfigPlace = configPlace;
            }

            public Guid GeneratorId { get; }

            public string MountPointUri { get; }

            public string ConfigPlace { get; }

            public string? LocaleConfigPlace { get; init; }

            public string? HostConfigPlace { get; init; }
        }
    }
}

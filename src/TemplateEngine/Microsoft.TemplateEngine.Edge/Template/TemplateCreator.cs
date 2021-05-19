// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
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
        public async Task<ITemplateCreationResult> InstantiateAsync(
            ITemplateInfo templateInfo,
            string? name,
            string? fallbackName,
            string? outputPath,
            IReadOnlyDictionary<string, string?> inputParameters,
            bool forceCreation = false,
            string? baselineName = null,
            bool dryRun = false,
            CancellationToken cancellationToken = default)
        {
            _ = templateInfo ?? throw new ArgumentNullException(nameof(templateInfo));
            inputParameters = inputParameters ?? new Dictionary<string, string?>();
            cancellationToken.ThrowIfCancellationRequested();

            ITemplate? template = LoadTemplate(templateInfo, baselineName);
            if (template == null)
            {
                return new TemplateCreationResult(CreationResultStatus.NotFound, templateInfo.Name, "Could not load template");
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
                if (!TryCreateParameterSet(template, realName!, inputParameters, out IParameterSet? effectParams, out TemplateCreationResult? resultIfParameterCreationFailed))
                {
                    //resultIfParameterCreationFailed is not null when TryCreateParameterSet is false
                    return resultIfParameterCreationFailed!;
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
                            "Destructive changes detected",
                            null,
                            null,
                            creationEffects);
                    }
                }

                if (!TryCreateParameterSet(template, realName!, inputParameters, out IParameterSet? creationParams, out resultIfParameterCreationFailed))
                {
                    return resultIfParameterCreationFailed!;
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
            catch (ContentGenerationException cx)
            {
                string message = cx.Message;
                if (cx.InnerException != null)
                {
                    message += Environment.NewLine + cx.InnerException;
                }
                return new TemplateCreationResult(
                    status: CreationResultStatus.CreateFailed,
                    templateName: template.Name,
                    localizedErrorMessage: message,
                    outputBaseDir: targetDir);
            }
            catch (Exception ex)
            {
                return new TemplateCreationResult(
                    status: CreationResultStatus.CreateFailed,
                    templateName: template.Name,
                    localizedErrorMessage: ex.Message,
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
        public bool AnyParametersWithInvalidDefaultsUnresolved(IReadOnlyList<string> defaultParamsWithInvalidValues, IReadOnlyList<string> userParamsWithInvalidValues, IReadOnlyDictionary<string, string?> inputParameters, out IReadOnlyList<string> invalidDefaultParameters)
        {
            invalidDefaultParameters = defaultParamsWithInvalidValues.Where(x => !inputParameters.ContainsKey(x)).ToList();
            return invalidDefaultParameters.Count > 0;
        }

        [Obsolete("The method is deprecated.")]
        //This method should become internal once Cli help logic is refactored.
        public void ReleaseMountPoints(ITemplate template)
        {
            if (template == null)
            {
                return;
            }

            if (template.LocaleConfiguration != null)
            {
                template.LocaleConfiguration.MountPoint.Dispose();
            }

            if (template.Configuration != null)
            {
                template.Configuration.MountPoint.Dispose();
            }

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
            ITemplateEngineHost host = _environmentSettings.Host;
            IParameterSet templateParams = templateInfo.Generator.GetParametersForTemplate(_environmentSettings, templateInfo);
            List<string> paramsWithInvalidValuesList = new List<string>();

            foreach (ITemplateParameter param in templateParams.ParameterDefinitions)
            {
                if (param.IsName)
                {
                    templateParams.ResolvedValues[param] = realName;
                }
                else if (host.TryGetHostParamDefault(param.Name, out string? hostParamValue) && hostParamValue != null)
                {
                    object resolvedValue = templateInfo.Generator.ConvertParameterValueToType(_environmentSettings, param, hostParamValue, out bool valueResolutionError);
                    if (!valueResolutionError)
                    {
                        templateParams.ResolvedValues[param] = resolvedValue;
                    }
                    else
                    {
                        paramsWithInvalidValuesList.Add(param.Name);
                    }
                }
                else if (param.Priority != TemplateParameterPriority.Required && param.DefaultValue != null)
                {
                    object resolvedValue = templateInfo.Generator.ConvertParameterValueToType(_environmentSettings, param, param.DefaultValue, out bool valueResolutionError);
                    if (!valueResolutionError)
                    {
                        templateParams.ResolvedValues[param] = resolvedValue;
                    }
                    else
                    {
                        paramsWithInvalidValuesList.Add(param.Name);
                    }
                }
            }

            paramsWithInvalidValues = paramsWithInvalidValuesList;
            return templateParams;
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
            List<string> tmpParamsWithInvalidValues = new List<string>();
            paramsWithInvalidValues = tmpParamsWithInvalidValues;

            foreach (KeyValuePair<string, string?> inputParam in inputParameters)
            {
                if (templateParams.TryGetParameterDefinition(inputParam.Key, out ITemplateParameter paramFromTemplate))
                {
                    if (inputParam.Value == null)
                    {
                        if (!string.IsNullOrEmpty(paramFromTemplate.DefaultIfOptionWithoutValue))
                        {
                            templateParams.ResolvedValues[paramFromTemplate] = template.Generator.ConvertParameterValueToType(_environmentSettings, paramFromTemplate, paramFromTemplate.DefaultIfOptionWithoutValue, out bool valueResolutionError);
                            // don't fail on value resolution errors, but report them as authoring problems.
                            if (valueResolutionError)
                            {
                                _logger.LogDebug($"Template {template.Identity} has an invalid DefaultIfOptionWithoutValue value for parameter {inputParam.Key}");
                            }
                        }
                        else
                        {
                            tmpParamsWithInvalidValues.Add(paramFromTemplate.Name);
                        }
                    }
                    else
                    {
                        templateParams.ResolvedValues[paramFromTemplate] = template.Generator.ConvertParameterValueToType(_environmentSettings, paramFromTemplate, inputParam.Value, out bool valueResolutionError);
                        if (valueResolutionError)
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
        public ITemplate? LoadTemplate(ITemplateInfo info, string? baselineName)
        {
            IGenerator? generator;
            if (!_environmentSettings.Components.TryGetComponent(info.GeneratorId, out generator))
            {
                return null;
            }
            IMountPoint? mountPoint;
            if (!_environmentSettings.TryGetMountPoint(info.MountPointUri, out mountPoint))
            {
                return null;
            }
            IFile config = mountPoint!.FileInfo(info.ConfigPlace);
            IFile? localeConfig = string.IsNullOrEmpty(info.LocaleConfigPlace) ? null : mountPoint.FileInfo(info.LocaleConfigPlace);
            IFile? hostTemplateConfigFile = string.IsNullOrEmpty(info.HostConfigPlace) ? null : mountPoint.FileInfo(info.HostConfigPlace);
            ITemplate template;
            using (Timing.Over(_environmentSettings.Host.Logger, $"Template from config {config.MountPoint.MountPointUri}{config.FullPath}"))
            {
                if (generator!.TryGetTemplateFromConfigInfo(config, out template, localeConfig, hostTemplateConfigFile, baselineName))
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

        /// <summary>
        /// Checks that all required parameters are provided. If a missing one is found, a value may be provided via host.OnParameterError
        /// but it's up to the caller / UI to decide how to act.
        /// Returns true if there are any missing params, false otherwise.
        /// </summary>
        /// <param name="templateParams"></param>
        /// <param name="missingParamNames"></param>
        /// <returns></returns>
        private bool CheckForMissingRequiredParameters(IParameterSet templateParams, out IList<string> missingParamNames)
        {
            ITemplateEngineHost host = _environmentSettings.Host;
            bool anyMissingParams = false;
            missingParamNames = new List<string>();

            foreach (ITemplateParameter parameter in templateParams.ParameterDefinitions)
            {
                if (parameter.Priority == TemplateParameterPriority.Required && !templateParams.ResolvedValues.ContainsKey(parameter))
                {
                    string newParamValue;
#pragma warning disable CS0618 // Type or member is obsolete - for backward compatibility
                    while (host.OnParameterError(parameter, "", "Missing required parameter", out newParamValue)
#pragma warning restore CS0618 // Type or member is obsolete
                        && string.IsNullOrEmpty(newParamValue))
                    {
                    }

                    if (!string.IsNullOrEmpty(newParamValue))
                    {
                        templateParams.ResolvedValues.Add(parameter, newParamValue);
                    }
                    else
                    {
                        missingParamNames.Add(parameter.Name);
                        anyMissingParams = true;
                    }
                }
            }

            return anyMissingParams;
        }

        private bool TryCreateParameterSet(ITemplate template, string realName, IReadOnlyDictionary<string, string?> inputParameters, out IParameterSet? templateParams, out TemplateCreationResult? failureResult)
        {
            // there should never be param errors here. If there are, the template is malformed, or the host gave an invalid value.
#pragma warning disable CS0618 // Type or member is obsolete - temporary until the method becomes internal.
            templateParams = SetupDefaultParamValuesFromTemplateAndHost(template, realName, out IReadOnlyList<string> defaultParamsWithInvalidValues);

            ResolveUserParameters(template, templateParams, inputParameters, out IReadOnlyList<string> userParamsWithInvalidValues);

            if (AnyParametersWithInvalidDefaultsUnresolved(defaultParamsWithInvalidValues, userParamsWithInvalidValues, inputParameters, out IReadOnlyList<string> defaultsWithUnresolvedInvalidValues)
#pragma warning restore CS0618 // Type or member is obsolete
                    || userParamsWithInvalidValues.Count > 0)
            {
                string message = string.Join(", ", new CombinedList<string>(userParamsWithInvalidValues, defaultsWithUnresolvedInvalidValues));
                failureResult = new TemplateCreationResult(CreationResultStatus.InvalidParamValues, template.Name, message);
                templateParams = null;
                return false;
            }

            bool missingParams = CheckForMissingRequiredParameters(templateParams, out IList<string> missingParamNames);

            if (missingParams)
            {
                failureResult = new TemplateCreationResult(CreationResultStatus.MissingMandatoryParam, template.Name, string.Join(", ", missingParamNames));
                templateParams = null;
                return false;
            }

            failureResult = null;
            return true;
        }
    }
}

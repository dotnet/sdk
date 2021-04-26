// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public class TemplateCreator
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly SettingsFilePaths _paths;

        public TemplateCreator(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new SettingsFilePaths(environmentSettings);
        }

        public Task<TemplateCreationResult> InstantiateAsync(ITemplateInfo templateInfo, string name, string fallbackName, string outputPath, IReadOnlyDictionary<string, string> inputParameters, bool skipUpdateCheck, bool forceCreation, string baselineName)
        {
            return InstantiateAsync(templateInfo, name, fallbackName, outputPath, inputParameters, skipUpdateCheck, forceCreation, baselineName, false);
        }

        public async Task<TemplateCreationResult> InstantiateAsync(ITemplateInfo templateInfo, string name, string fallbackName, string outputPath, IReadOnlyDictionary<string, string> inputParameters, bool skipUpdateCheck, bool forceCreation, string baselineName, bool dryRun)
        {
            // SettingsLoader.LoadTemplate is where the loc info should be read!!!
            // templateInfo knows enough to get at the loc, if any
            ITemplate template = _environmentSettings.SettingsLoader.LoadTemplate(templateInfo, baselineName);

            try
            {
                if (template == null)
                {
                    return new TemplateCreationResult("Could not load template", CreationResultStatus.NotFound, templateInfo.Name);
                }

                string realName = name ?? fallbackName ?? template.DefaultName;

                if (string.IsNullOrEmpty(realName))
                {
                    return new TemplateCreationResult("--name", CreationResultStatus.MissingMandatoryParam, template.Name);
                }

                if (template.IsNameAgreementWithFolderPreferred && string.IsNullOrEmpty(outputPath))
                {
                    outputPath = name;
                }

                ICreationResult creationResult = null;
                string targetDir = outputPath ?? _environmentSettings.Host.FileSystem.GetCurrentDirectory();

                try
                {
                    if (!dryRun)
                    {
                        _paths.CreateDirectory(targetDir);
                    }

                    Stopwatch sw = Stopwatch.StartNew();
                    IComponentManager componentManager = _environmentSettings.SettingsLoader.Components;

                    // setup separate sets of parameters to be used for GetCreationEffects() and by CreateAsync().
                    if (!TryCreateParameterSet(template, realName, inputParameters, out IParameterSet effectParams, out TemplateCreationResult resultIfParameterCreationFailed))
                    {
                        return resultIfParameterCreationFailed;
                    }

                    ICreationEffects creationEffects = template.Generator.GetCreationEffects(_environmentSettings, template, effectParams, componentManager, targetDir);
                    IReadOnlyList<IFileChange> changes = creationEffects.FileChanges;
                    IReadOnlyList<IFileChange> destructiveChanges = changes.Where(x => x.ChangeKind != ChangeKind.Create).ToList();

                    if (!forceCreation && destructiveChanges.Count > 0)
                    {
                        if (!_environmentSettings.Host.OnPotentiallyDestructiveChangesDetected(changes, destructiveChanges))
                        {
                            return new TemplateCreationResult("Cancelled", CreationResultStatus.Cancelled, template.Name);
                        }
                    }

                    if (!TryCreateParameterSet(template, realName, inputParameters, out IParameterSet creationParams, out resultIfParameterCreationFailed))
                    {
                        return resultIfParameterCreationFailed;
                    }

                    if (!dryRun)
                    {
                        creationResult = await template.Generator.CreateAsync(_environmentSettings, template, creationParams, componentManager, targetDir).ConfigureAwait(false);
                    }

                    sw.Stop();
                    _environmentSettings.Host.LogTiming("Content generation time", sw.Elapsed, 0);
                    return new TemplateCreationResult(string.Empty, CreationResultStatus.Success, template.Name, creationResult, targetDir, creationEffects);
                }
                catch (ContentGenerationException cx)
                {
                    string message = cx.Message;
                    if (cx.InnerException != null)
                    {
                        message += Environment.NewLine + cx.InnerException;
                    }

                    return new TemplateCreationResult(message, CreationResultStatus.CreateFailed, template.Name);
                }
                catch (Exception ex)
                {
                    return new TemplateCreationResult(ex.Message, CreationResultStatus.CreateFailed, template.Name);
                }
            }
            finally
            {
                ReleaseMountPoints(template);
            }
        }

        public bool AnyParametersWithInvalidDefaultsUnresolved(IReadOnlyList<string> defaultParamsWithInvalidValues, IReadOnlyList<string> userParamsWithInvalidValues, IReadOnlyDictionary<string, string> inputParameters, out IReadOnlyList<string> invalidDefaultParameters)
        {
            invalidDefaultParameters = defaultParamsWithInvalidValues.Where(x => !inputParameters.ContainsKey(x)).ToList();
            return invalidDefaultParameters.Count > 0;
        }

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

        //
        // Reads the parameters from the template and the host and setup their values in the return IParameterSet.
        // Host param values override template defaults.
        //
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
                else if (host.TryGetHostParamDefault(param.Name, out string hostParamValue) && hostParamValue != null)
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

        //
        // The template params for which there are same-named input parameters have their values set to the corresponding input parameters value.
        // input parameters that do not have corresponding template params are ignored.
        //
        public void ResolveUserParameters(ITemplate template, IParameterSet templateParams, IReadOnlyDictionary<string, string> inputParameters, out IReadOnlyList<string> paramsWithInvalidValues)
        {
            List<string> tmpParamsWithInvalidValues = new List<string>();
            paramsWithInvalidValues = tmpParamsWithInvalidValues;

            foreach (KeyValuePair<string, string> inputParam in inputParameters)
            {
                if (templateParams.TryGetParameterDefinition(inputParam.Key, out ITemplateParameter paramFromTemplate))
                {
                    if (inputParam.Value == null)
                    {
                        if (paramFromTemplate is IAllowDefaultIfOptionWithoutValue paramFromTemplateWithNoValueDefault
                            && !string.IsNullOrEmpty(paramFromTemplateWithNoValueDefault.DefaultIfOptionWithoutValue))
                        {
                            templateParams.ResolvedValues[paramFromTemplate] = template.Generator.ConvertParameterValueToType(_environmentSettings, paramFromTemplate, paramFromTemplateWithNoValueDefault.DefaultIfOptionWithoutValue, out bool valueResolutionError);
                            // don't fail on value resolution errors, but report them as authoring problems.
                            if (valueResolutionError)
                            {
                                _environmentSettings.Host.LogDiagnosticMessage($"Template {template.Identity} has an invalid DefaultIfOptionWithoutValue value for parameter {inputParam.Key}", "Authoring");
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

        //
        // Checks that all required parameters are provided. If a missing one is found, a value may be provided via host.OnParameterError
        // but it's up to the caller / UI to decide how to act.
        // Returns true if there are any missing params, false otherwise.
        //
        public bool CheckForMissingRequiredParameters(IParameterSet templateParams, out IList<string> missingParamNames)
        {
            ITemplateEngineHost host = _environmentSettings.Host;
            bool anyMissingParams = false;
            missingParamNames = new List<string>();

            foreach (ITemplateParameter parameter in templateParams.ParameterDefinitions)
            {
                if (parameter.Priority == TemplateParameterPriority.Required && !templateParams.ResolvedValues.ContainsKey(parameter))
                {
                    string newParamValue;
                    while (host.OnParameterError(parameter, null, "Missing required parameter", out newParamValue)
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

        private bool TryCreateParameterSet(ITemplate template, string realName, IReadOnlyDictionary<string, string> inputParameters, out IParameterSet templateParams, out TemplateCreationResult failureResult)
        {
            // there should never be param errors here. If there are, the template is malformed, or the host gave an invalid value.
            templateParams = SetupDefaultParamValuesFromTemplateAndHost(template, realName, out IReadOnlyList<string> defaultParamsWithInvalidValues);
            ResolveUserParameters(template, templateParams, inputParameters, out IReadOnlyList<string> userParamsWithInvalidValues);

            if (AnyParametersWithInvalidDefaultsUnresolved(defaultParamsWithInvalidValues, userParamsWithInvalidValues, inputParameters, out IReadOnlyList<string> defaultsWithUnresolvedInvalidValues)
                    || userParamsWithInvalidValues.Count > 0)
            {
                string message = string.Join(", ", new CombinedList<string>(userParamsWithInvalidValues, defaultsWithUnresolvedInvalidValues));
                failureResult = new TemplateCreationResult(message, CreationResultStatus.InvalidParamValues, template.Name);
                templateParams = null;
                return false;
            }

            bool missingParams = CheckForMissingRequiredParameters(templateParams, out IList<string> missingParamNames);

            if (missingParams)
            {
                failureResult = new TemplateCreationResult(string.Join(", ", missingParamNames), CreationResultStatus.MissingMandatoryParam, template.Name);
                templateParams = null;
                return false;
            }

            failureResult = null;
            return true;
        }
    }
}

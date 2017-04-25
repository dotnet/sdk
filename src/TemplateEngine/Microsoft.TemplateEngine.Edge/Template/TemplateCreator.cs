using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Edge.Template
{
    public class TemplateCreator
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly Paths _paths;

        public TemplateCreator(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _paths = new Paths(environmentSettings);
        }

        public async Task<TemplateCreationResult> InstantiateAsync(ITemplateInfo templateInfo, string name, string fallbackName, string outputPath, IReadOnlyDictionary<string, string> inputParameters, bool skipUpdateCheck, bool forceCreation, string baselineName)
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

                // there should never be param errors here. If there are, the template is malformed, or the host gave an invalid value.
                IParameterSet templateParams = SetupDefaultParamValuesFromTemplateAndHost(template, realName, out IReadOnlyList<string> defaultParamsWithInvalidValues);
                ResolveUserParameters(template, templateParams, inputParameters, out IReadOnlyList<string> userParamsWithInvalidValues);

                if (AnyParametersWithInvalidDefaultsUnresolved(defaultParamsWithInvalidValues, userParamsWithInvalidValues, inputParameters, out IReadOnlyList<string> defaultsWithUnresolvedInvalidValues)
                        || userParamsWithInvalidValues.Count > 0)
                {
                    string message = string.Join(", ", new CombinedList<string>(userParamsWithInvalidValues, defaultsWithUnresolvedInvalidValues));
                    return new TemplateCreationResult(message, CreationResultStatus.InvalidParamValues, template.Name);
                }

                bool missingParams = CheckForMissingRequiredParameters(templateParams, out IList<string> missingParamNames);

                if (missingParams)
                {
                    return new TemplateCreationResult(string.Join(", ", missingParamNames), CreationResultStatus.MissingMandatoryParam, template.Name);
                }

                if (template.IsNameAgreementWithFolderPreferred && string.IsNullOrEmpty(outputPath))
                {
                    outputPath = name;
                }

                ICreationResult creationResult = null;
                string targetDir = outputPath ?? _environmentSettings.Host.FileSystem.GetCurrentDirectory();

                try
                {
                    _paths.CreateDirectory(targetDir);
                    Stopwatch sw = Stopwatch.StartNew();
                    IComponentManager componentManager = _environmentSettings.SettingsLoader.Components;

                    IReadOnlyList<IFileChange> changes = template.Generator.GetCreationEffects(_environmentSettings, template, templateParams, componentManager, targetDir).FileChanges;
                    IReadOnlyList<IFileChange> destructiveChanges = changes.Where(x => x.ChangeKind != ChangeKind.Create).ToList();

                    if (!forceCreation && destructiveChanges.Count > 0)
                    {
                        if (!_environmentSettings.Host.OnPotentiallyDestructiveChangesDetected(changes, destructiveChanges))
                        {
                            return new TemplateCreationResult("Cancelled", CreationResultStatus.Cancelled, template.Name);
                        }
                    }

                    creationResult = await template.Generator.CreateAsync(_environmentSettings, template, templateParams, componentManager, targetDir).ConfigureAwait(false);
                    sw.Stop();
                    _environmentSettings.Host.LogTiming("Content generation time", sw.Elapsed, 0);
                    return new TemplateCreationResult(string.Empty, CreationResultStatus.Success, template.Name, creationResult, targetDir);
                }
                catch (ContentGenerationException cx)
                {
                    string message = cx.Message;
                    if (cx.InnerException != null)
                    {
                        message += Environment.NewLine + cx.InnerException.Message;
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
            if(template.LocaleConfiguration != null)
            {
                _environmentSettings.SettingsLoader.ReleaseMountPoint(template.LocaleConfiguration.MountPoint);
            }

            if (template.Configuration != null)
            {
                _environmentSettings.SettingsLoader.ReleaseMountPoint(template.Configuration.MountPoint);
            }

            if (template.TemplateSourceRoot != null && template.TemplateSourceRoot != template.Configuration)
            {
                _environmentSettings.SettingsLoader.ReleaseMountPoint(template.TemplateSourceRoot.MountPoint);
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
                    // The user provided params included the name of a bool flag without a value.
                    // We assume that means the value "true" is desired for the bool.
                    // This must happen here, as opposed to GlobalRunSpec.ProduceUserVariablesCollection()
                    if (inputParam.Value == null)
                    {
                        if (paramFromTemplate.DataType == "bool")
                        {
                            // could probably directly assign bool true here, but best to have everything go through the same process
                            // ... in case something changes downstream.
                            // ignore the valueResolutionError
                            templateParams.ResolvedValues[paramFromTemplate] = template.Generator.ConvertParameterValueToType(_environmentSettings, paramFromTemplate, "true", out bool valueResolutionError);
                        }
                        else
                        {
                            throw new TemplateParamException(inputParam.Key, null, paramFromTemplate.DataType);
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

        //
        // Direct copy of commented out code from the old TemplateCreator.Instantiate()
        // This won't build, much less work, but it will be revived someday.
        //
        //public void UpdateCheck()
        //{
        //    //TODO: Implement check for updates over mount points
        //    bool updatesReady;

        //    if (tmplt.Source.ParentSource != null)
        //    {
        //        updatesReady = await tmplt.Source.Source.CheckForUpdatesAsync(tmplt.Source.ParentSource, tmplt.Source.Location);
        //    }
        //    else
        //    {
        //        updatesReady = await tmplt.Source.Source.CheckForUpdatesAsync(tmplt.Source.Location);
        //    }

        //    if (updatesReady)
        //    {
        //        Console.WriteLine("Updates for this template are available. Install them now? [Y]");
        //        string answer = Console.ReadLine();

        //        if (string.IsNullOrEmpty(answer) || answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
        //        {
        //            string packageId = tmplt.Source.ParentSource != null
        //                ? tmplt.Source.Source.GetInstallPackageId(tmplt.Source.ParentSource, tmplt.Source.Location)
        //                : tmplt.Source.Source.GetInstallPackageId(tmplt.Source.Location);

        //            Command.CreateDotNet("new3", new[] { "-u", packageId, "--quiet" }).ForwardStdOut().ForwardStdErr().Execute();
        //            Command.CreateDotNet("new3", new[] { "-i", packageId, "--quiet" }).ForwardStdOut().ForwardStdErr().Execute();

        //            Program.Broker.ComponentRegistry.ForceReinitialize();

        //            if (!TryGetTemplate(templateName, source, quiet, out tmplt))
        //            {
        //                return -1;
        //            }

        //            generator = tmplt.Generator;
        //        }
        //    }
        //}
    }
}

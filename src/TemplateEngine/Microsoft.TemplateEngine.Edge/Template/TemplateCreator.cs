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
        private readonly AliasRegistry _aliasRegistry;
        private readonly Paths _paths;

        public TemplateCreator(IEngineEnvironmentSettings environmentSettings)
        {
            _environmentSettings = environmentSettings;
            _aliasRegistry = new AliasRegistry(environmentSettings);
            _paths = new Paths(environmentSettings);
        }

        public async Task<TemplateCreationResult> InstantiateAsync(ITemplateInfo templateInfo, string name, string fallbackName, string outputPath, IReadOnlyDictionary<string, string> inputParameters, bool skipUpdateCheck, bool forceCreation)
        {
            // SettingsLoader.LoadTemplate is where the loc info should be read!!!
            // templateInfo knows enough to get at the loc, if any
            ITemplate template = _environmentSettings.SettingsLoader.LoadTemplate(templateInfo);

            if (template == null)
            {
                return new TemplateCreationResult("Could not load template", CreationResultStatus.NotFound, templateInfo.Name);
            }

            string realName = name ?? template.DefaultName ?? fallbackName;
            // there should never be param errors here. If there are, the template is malformed, or the host gave an invalid value.
            IParameterSet templateParams = SetupDefaultParamValuesFromTemplateAndHost(template, realName, out IList<string> defaultParamsWithInvalidValues);
            if (defaultParamsWithInvalidValues.Any())
            {
                string message = string.Join(", ", defaultParamsWithInvalidValues);
                return new TemplateCreationResult(message, CreationResultStatus.InvalidParamValues, template.Name);
            }

            ResolveUserParameters(template, templateParams, inputParameters, out IList<string> userParamsWithInvalidValues);
            if (userParamsWithInvalidValues.Any())
            {
                string message = string.Join(", ", userParamsWithInvalidValues);
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

                IReadOnlyList<IFileChange> changes = template.Generator.GetFileChanges(_environmentSettings, template, templateParams, componentManager, targetDir);
                IReadOnlyList<IFileChange> destructiveChanges = changes.Where(x => x.ChangeKind != ChangeKind.Create).ToList();

                if (!forceCreation && destructiveChanges.Count > 0)
                {
                    if(!_environmentSettings.Host.OnPotentiallyDestructiveChangesDetected(changes, destructiveChanges))
                    {
                        return new TemplateCreationResult("Cancelled", CreationResultStatus.Cancelled, template.Name);
                    }
                }

                creationResult = await template.Generator.CreateAsync(_environmentSettings, template, templateParams, componentManager, targetDir).ConfigureAwait(false);
                sw.Stop();
                _environmentSettings.Host.OnTimingCompleted("Content generation time", sw.Elapsed);
                return new TemplateCreationResult(string.Empty, CreationResultStatus.Success, template.Name, creationResult, outputPath);
            }                
            catch (ContentGenerationException cx)
            {
                string message = cx.Message;
                if(cx.InnerException != null)
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

        // 
        // Reads the parameters from the template and the host and setup their values in the return IParameterSet.
        // Host param values override template defaults.
        //
        public IParameterSet SetupDefaultParamValuesFromTemplateAndHost(ITemplate templateInfo, string realName, out IList<string> paramsWithInvalidValues)
        {
            ITemplateEngineHost host = _environmentSettings.Host;
            IParameterSet templateParams = templateInfo.Generator.GetParametersForTemplate(_environmentSettings, templateInfo);
            paramsWithInvalidValues = new List<string>();

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
                        paramsWithInvalidValues.Add(param.Name);
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
                        paramsWithInvalidValues.Add(param.Name);
                    }
                }
            }

            return templateParams;
        }

        // 
        // The template params for which there are same-named input parameters have their values set to the corresponding input parameters value.
        // input parameters that do not have corresponding template params are ignored.
        //
        public void ResolveUserParameters(ITemplate template, IParameterSet templateParams, IReadOnlyDictionary<string, string> inputParameters, out IList<string> paramsWithInvalidValues)
        {
            paramsWithInvalidValues = new List<string>();

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
                            paramsWithInvalidValues.Add(paramFromTemplate.Name);
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

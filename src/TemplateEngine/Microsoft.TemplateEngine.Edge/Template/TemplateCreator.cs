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
    public static class TemplateCreator
    {
        // returns the templates whose:
        //      name or shortName contains the searchString
        //      matches by a registered alias
        public static IReadOnlyCollection<ITemplateInfo> List(string searchString, string language)
        {
            HashSet<ITemplateInfo> matchingTemplates = new HashSet<ITemplateInfo>(TemplateEqualityComparer.Default);
            HashSet<ITemplateInfo> allTemplates = new HashSet<ITemplateInfo>(TemplateEqualityComparer.Default);

            using (Timing.Over("load"))
                SettingsLoader.GetTemplates(allTemplates);

#if !NET45
            IReadOnlyCollection<ITemplateInfo> allTemplatesCollection = allTemplates;
#else
            IReadOnlyCollection<ITemplateInfo> allTemplatesCollection = allTemplates.ToList();
#endif

            IReadOnlyCollection<ITemplateInfo> aliasSearchResult;

            using (Timing.Over("Alias search"))
            {
                aliasSearchResult = AliasRegistry.GetTemplatesForAlias(searchString, allTemplatesCollection);

                if (!string.IsNullOrEmpty(language))
                {
                    aliasSearchResult = aliasSearchResult.Where(x =>
                    {
                        return x.Tags == null || !x.Tags.TryGetValue("language", out string langVal) || string.Equals(langVal, language, StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }

                if(aliasSearchResult.Count == 1)
                {
                    return aliasSearchResult;
                }
            }

            using (Timing.Over("Search in loaded"))
            {
                string[] tagParts = searchString?.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                List<ITemplateInfo> exactMatches = new List<ITemplateInfo>();
                foreach (ITemplateInfo template in allTemplates)
                {
                    if (!string.IsNullOrEmpty(language))
                    {
                        if (template.Tags != null && template.Tags.TryGetValue("language", out string langVal) && !string.Equals(langVal, language, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    if (string.IsNullOrEmpty(searchString))
                    {
                        matchingTemplates.Add(template);
                    }
                    else
                    {
                        int nameCompare = template.Name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase);
                        int shortNameCompare = template.ShortName.IndexOf(searchString, StringComparison.OrdinalIgnoreCase);

                        if (nameCompare == 0 && string.Equals(template.Name, searchString, StringComparison.OrdinalIgnoreCase) 
                            || shortNameCompare == 0 && string.Equals(template.ShortName, searchString, StringComparison.OrdinalIgnoreCase))
                        {
                            exactMatches.Add(template);
                        }

                        if(nameCompare > -1 || shortNameCompare > -1)
                        {
                            matchingTemplates.Add(template);
                        }

                        if (template.Classifications != null && template.Classifications.Count > 0)
                        {
                            if (tagParts.All(x => template.Classifications.Contains(x, StringComparer.OrdinalIgnoreCase)))
                            {
                                matchingTemplates.Add(template);
                            }
                        }
                        else
                        {
                            matchingTemplates.Add(template);
                        }
                    }
                }

                if (exactMatches.Count > 0)
                {
                    return exactMatches;
                }
            }

            matchingTemplates.UnionWith(aliasSearchResult);

#if !NET45
            IReadOnlyCollection<ITemplateInfo> matchingTemplatesCollection = matchingTemplates;
#else
            IReadOnlyCollection<ITemplateInfo> matchingTemplatesCollection = matchingTemplates.ToList();
#endif
            return matchingTemplatesCollection;
        }

        public static async Task<TemplateCreationResult> InstantiateAsync(ITemplateInfo templateInfo, string name, string fallbackName, string outputPath, IReadOnlyDictionary<string, string> inputParameters, bool skipUpdateCheck)
        {
            // SettingsLoader.LoadTemplate is where the loc info should be read!!!
            // templateInfo knows enough to get at the loc, if any
            ITemplate template = SettingsLoader.LoadTemplate(templateInfo);

            string realName = name ?? template.DefaultName ?? fallbackName;
            // there should never be param errors here. If there are, the template is malformed, or the host gave an invalid value.
            IParameterSet templateParams = SetupDefaultParamValuesFromTemplateAndHost(template, realName, out IList<string> defaultParamsWithInvalidValues);
            if (defaultParamsWithInvalidValues.Any())
            {
                string message = string.Join(", ", defaultParamsWithInvalidValues);
                return new TemplateCreationResult(-1, message, CreationResultStatus.InvalidParamValues, template.Name);
            }

            ResolveUserParameters(template, templateParams, inputParameters, out IList<string> userParamsWithInvalidValues);
            if (userParamsWithInvalidValues.Any())
            {
                string message = string.Join(", ", userParamsWithInvalidValues);
                return new TemplateCreationResult(-1, message, CreationResultStatus.InvalidParamValues, template.Name);
            }

            bool missingParams = CheckForMissingRequiredParameters(templateParams, out IList<string> missingParamNames);

            if (missingParams)
            {
                return new TemplateCreationResult(-1, string.Join(", ", missingParamNames), CreationResultStatus.MissingMandatoryParam, template.Name);
            }

            if (template.IsNameAgreementWithFolderPreferred && string.IsNullOrEmpty(outputPath))
            {
                outputPath = name;
            }

            ICreationResult creationResult = null;

            try
            {
                string targetDir = outputPath ?? EngineEnvironmentSettings.Host.FileSystem.GetCurrentDirectory();
                targetDir.CreateDirectory();
                Stopwatch sw = Stopwatch.StartNew();
                IComponentManager componentManager = SettingsLoader.Components;
                creationResult = await template.Generator.CreateAsync(template, templateParams, componentManager, targetDir).ConfigureAwait(false);
                sw.Stop();
                EngineEnvironmentSettings.Host.OnTimingCompleted("Content generation time", sw.Elapsed);
            }
            finally
            {
            }

            if (creationResult != null)
            {
                return new TemplateCreationResult(0, string.Empty, CreationResultStatus.CreateSucceeded, template.Name, creationResult);
            }

            return new TemplateCreationResult(0, string.Empty, CreationResultStatus.CreateFailed, template.Name);
        }

        // 
        // Reads the parameters from the template and the host and setup their values in the return IParameterSet.
        // Host param values override template defaults.
        //
        public static IParameterSet SetupDefaultParamValuesFromTemplateAndHost(ITemplate template, string realName, out IList<string> paramsWithInvalidValues)
        {
            ITemplateEngineHost host = EngineEnvironmentSettings.Host;
            IParameterSet templateParams = template.Generator.GetParametersForTemplate(template);
            paramsWithInvalidValues = new List<string>();

            foreach (ITemplateParameter param in templateParams.ParameterDefinitions)
            {
                if (param.IsName)
                {
                    templateParams.ResolvedValues[param] = realName;
                }
                else if (host.TryGetHostParamDefault(param.Name, out string hostParamValue) && hostParamValue != null)
                {
                    object resolvedValue = template.Generator.ConvertParameterValueToType(param, hostParamValue, out bool valueResolutionError);
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
                    object resolvedValue = template.Generator.ConvertParameterValueToType(param, param.DefaultValue, out bool valueResolutionError);
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
        public static void ResolveUserParameters(ITemplate template, IParameterSet templateParams, IReadOnlyDictionary<string, string> inputParameters, out IList<string> paramsWithInvalidValues)
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
                            templateParams.ResolvedValues[paramFromTemplate] = template.Generator.ConvertParameterValueToType(paramFromTemplate, "true", out bool valueResolutionError);
                        }
                        else
                        {
                            throw new TemplateParamException(inputParam.Key, null, paramFromTemplate.DataType);
                        }
                    }
                    else
                    {
                        templateParams.ResolvedValues[paramFromTemplate] = template.Generator.ConvertParameterValueToType(paramFromTemplate, inputParam.Value, out bool valueResolutionError);
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
        public static bool CheckForMissingRequiredParameters(IParameterSet templateParams, out IList<string> missingParamNames)
        {
            ITemplateEngineHost host = EngineEnvironmentSettings.Host;
            bool anyMissingParams = false;
            missingParamNames = new List<string>();

            foreach (ITemplateParameter parameter in templateParams.ParameterDefinitions)
            {
                if (parameter.Priority == TemplateParameterPriority.Required && !templateParams.ResolvedValues.ContainsKey(parameter))
                {
                    while (host.OnParameterError(parameter, null, "Missing required parameter", out string newParamValue)
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
        //public static void UpdateCheck()
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

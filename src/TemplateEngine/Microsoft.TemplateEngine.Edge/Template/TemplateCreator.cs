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
        public static IReadOnlyCollection<ITemplateInfo> List(string searchString)
        {
            HashSet<ITemplateInfo> matchingTemplates = new HashSet<ITemplateInfo>(TemplateEqualityComparer.Default);
            HashSet<ITemplateInfo> allTemplates = new HashSet<ITemplateInfo>(TemplateEqualityComparer.Default);

            using (Timing.Over("load"))
                SettingsLoader.GetTemplates(allTemplates);

            using (Timing.Over("Search in loaded"))
                foreach (ITemplateInfo template in allTemplates)
                {
                    if (string.IsNullOrEmpty(searchString)
                        || template.Name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) > -1
                        || template.ShortName.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        matchingTemplates.Add(template);
                    }
                }

            using (Timing.Over("Alias search"))
            {
#if !NET451
                IReadOnlyCollection<ITemplateInfo> allTemplatesCollection = allTemplates;
#else
                IReadOnlyCollection<ITemplateInfo> allTemplatesCollection = allTemplates.ToList();
#endif
                matchingTemplates.UnionWith(AliasRegistry.GetTemplatesForAlias(searchString, allTemplatesCollection));
            }

#if !NET451
            IReadOnlyCollection<ITemplateInfo> matchingTemplatesCollection = matchingTemplates;
#else
            IReadOnlyCollection<ITemplateInfo> matchingTemplatesCollection = matchingTemplates.ToList();
#endif
            return matchingTemplatesCollection;
        }


        public static bool TryGetTemplateInfoFromCache(string templateName, out ITemplateInfo tmplt)
        {
            try
            {
                using (Timing.Over("List"))
                {
                    IReadOnlyCollection<ITemplateInfo> result = List(templateName);

                    if (result.Count == 1)
                    {
                        tmplt = result.First();
                        return true;
                    }
                }
            }
            catch
            {
            }

            tmplt = null;
            return false;
        }

        public static async Task<int> InstantiateAsync(string templateName, string name, string fallbackName, bool createDir, string aliasName, IReadOnlyDictionary<string, string> inputParameters, bool skipUpdateCheck)
        {
            ITemplateEngineHost host = EngineEnvironmentSettings.Host;
            ITemplateInfo templateInfo;

            using (Timing.Over("Get single"))
            {
                if (!TryGetTemplateInfoFromCache(templateName, out templateInfo))
                {
                    return -1;
                }
            }

            // SettingsLoader.LoadTemplate is where the loc info should be read!!!
            // templateInfo knows enough to get at the loc, if any
            ITemplate template = SettingsLoader.LoadTemplate(templateInfo);

            if (!skipUpdateCheck)
            {
                EngineEnvironmentSettings.Host.LogMessage("Checking for updates...");

                //UpdateCheck();    // this'll need params
            }

            string realName = name ?? template.DefaultName ?? fallbackName;
            IParameterSet templateParams = SetupDefaultParamValuesFromTemplateAndHost(template, realName);

            if (aliasName != null)
            {
                //TODO: Add parameters to aliases (from _parameters_ collection)
                AliasRegistry.SetTemplateAlias(aliasName, template);
                EngineEnvironmentSettings.Host.LogMessage("Alias created.");
                return 0;
            }

            ResolveUserParameters(template, templateParams, inputParameters);
            bool missingParams = CheckForMissingRequiredParameters(templateParams);

            if (missingParams)
            {
                return missingParams ? -1 : 0;
            }

            ICreationResult creationResult;

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                IComponentManager componentManager = Settings.SettingsLoader.Components;
                await template.Generator.Create(template, templateParams, componentManager, out creationResult);
                sw.Stop();
                EngineEnvironmentSettings.Host.OnTimingCompleted("Content generation time", sw.Elapsed);
            }
            finally
            {
            }

            // TODO: pass back the creationResult (probably as an out param)
            // (and get rid of this debugging)
            creationResult.TEMP_CONSOLE_DEBUG_CreationResult();

            return 0;
        }

        // 
        // Reads the parameters from the template and the host and setup their values in the return IParameterSet.
        // Host param values override template defaults.
        //
        public static IParameterSet SetupDefaultParamValuesFromTemplateAndHost(ITemplate template, string realName)
        {
            ITemplateEngineHost host = EngineEnvironmentSettings.Host;
            IParameterSet templateParams = template.Generator.GetParametersForTemplate(template);
            foreach (ITemplateParameter param in templateParams.ParameterDefinitions)
            {
                if (param.IsName)
                {
                    templateParams.ResolvedValues[param] = realName;
                }
                else if (host.TryGetHostParamDefault(param.Name, out string hostParamValue) && hostParamValue != null)
                {
                    templateParams.ResolvedValues[param] = template.Generator.ConvertParameterValueToType(param, hostParamValue);
                }
                else if (param.Priority != TemplateParameterPriority.Required && param.DefaultValue != null)
                {
                    templateParams.ResolvedValues[param] = template.Generator.ConvertParameterValueToType(param, param.DefaultValue);
                }
            }

            return templateParams;
        }

        // 
        // The template params for which there are same-named input parameters have their values set to the corresponding input paramers value.
        // input parameters that do not have corresponding template params are ignored.
        //
        public static void ResolveUserParameters(ITemplate template, IParameterSet templateParams, IReadOnlyDictionary<string, string> inputParameters)
        {
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
                            templateParams.ResolvedValues[paramFromTemplate] = template.Generator.ConvertParameterValueToType(paramFromTemplate, "true");
                        }
                        else
                        {
                            throw new TemplateParamException(inputParam.Key, null, paramFromTemplate.DataType);
                        }
                    }
                    else
                    {
                        templateParams.ResolvedValues[paramFromTemplate] = template.Generator.ConvertParameterValueToType(paramFromTemplate, inputParam.Value);
                    }
                }
            }
        }

        //
        // Checks that all required parameters are provided. If a missing one is found, a value may be provided via host.OnParameterError
        // but it's up to the caller / UI to decide how to act.
        // Returns true if there are any missing params, false otherwise.
        //
        public static bool CheckForMissingRequiredParameters(IParameterSet templateParams)
        {
            ITemplateEngineHost host = EngineEnvironmentSettings.Host;
            bool missingParams = false;

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
                        missingParams = true;
                    }
                }
            }

            return missingParams;
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

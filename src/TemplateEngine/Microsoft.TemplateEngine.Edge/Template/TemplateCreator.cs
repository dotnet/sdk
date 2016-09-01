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


        public static bool TryGetTemplate(string templateName, out ITemplateInfo tmplt)
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

        public static async Task<int> Instantiate(ITemplateEngineHost host, string templateName, string name, string fallbackName, bool createDir, string aliasName, IReadOnlyDictionary<string, string> inputParameters, bool skipUpdateCheck)
        {
            ITemplateInfo templateInfo;

            using (Timing.Over("Get single"))
            {
                if (! TryGetTemplate(templateName, out templateInfo))
                {
                    return -1;
                }
            }

            ITemplate template = SettingsLoader.LoadTemplate(templateInfo);

            if (!skipUpdateCheck)
            {
                host.LogMessage("Checking for updates...");

                //UpdateCheck();    // this'll need params
            }

            string realName = name ?? template.DefaultName ?? fallbackName;
            IParameterSet templateParams = SetupDefaultParamValuesFromTemplateAndHost(host, template, realName);

            if (aliasName != null)
            {
                //TODO: Add parameters to aliases (from _parameters_ collection)
                AliasRegistry.SetTemplateAlias(aliasName, template);
                host.LogMessage("Alias created.");
                return 0;
            }

            ResolveUserParameters(templateParams, inputParameters);
            bool missingParams = CheckForMissingRequiredParameters(host, templateParams);

            if (missingParams)
            {
                return missingParams ? -1 : 0;
            }

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                // todo: pass an implementation of ITemplateEngineHost 
                await template.Generator.Create(host, template, templateParams);
                sw.Stop();
                host.OnTimingCompleted("Content generation time", sw.Elapsed);
            }
            finally
            {
            }

            return 0;
        }

        // 
        // Reads the parameters from the template and the host and setup their values in the return IParameterSet.
        // Host param values override template defaults.
        //
        public static IParameterSet SetupDefaultParamValuesFromTemplateAndHost(ITemplateEngineHost host, ITemplate template, string realName)
        {
            IParameterSet templateParams = template.Generator.GetParametersForTemplate(template);
            string hostParamValue;

            foreach (ITemplateParameter param in templateParams.ParameterDefinitions)
            {
                if (param.IsName)
                {
                    templateParams.ResolvedValues[param] = realName;
                }
                else if (host.TryGetHostParamDefault(param.Name, out hostParamValue) && hostParamValue != null)
                {
                    templateParams.ResolvedValues[param] = hostParamValue;
                }
                else if (param.Priority != TemplateParameterPriority.Required && param.DefaultValue != null)
                {
                    templateParams.ResolvedValues[param] = param.DefaultValue;
                }
            }

            return templateParams;
        }

        // 
        // The template params for which there are same-named input parameters have their values set to the corresponding input paramers value.
        // input parameters that do not have corresponding template params are ignored.
        //
        public static void ResolveUserParameters(IParameterSet templateParams, IReadOnlyDictionary<string, string> inputParameters)
        {
            foreach (KeyValuePair<string, string> inputParam in inputParameters)
            {
                ITemplateParameter paramFromTemplate;
                if (templateParams.TryGetParameterDefinition(inputParam.Key, out paramFromTemplate))
                {
                    // The user provided params included the name of a bool flag without a value.
                    // We assume that means the value "true" is desired for the bool.
                    // This must happen here, as opposed to GlobalRunSpec.ProduceUserVariablesCollection()
                    if (inputParam.Value == null)
                    {
                        if (paramFromTemplate.DataType == "bool")
                        {
                            templateParams.ResolvedValues[paramFromTemplate] = "true";
                        }
                        else
                        {
                            throw new TemplateParamException(inputParam.Key, null, paramFromTemplate.DataType);
                        }
                    }
                    else
                    {
                        templateParams.ResolvedValues[paramFromTemplate] = inputParam.Value;
                    }
                }
            }
        }

        //
        // Checks that all required parameters are provided. If a missing one is found, a value may be provided via host.OnParameterError
        // but it's up to the caller / UI to decide how to act.
        // Returns true if there are any missing params, false otherwise.
        //
        public static bool CheckForMissingRequiredParameters(ITemplateEngineHost host, IParameterSet templateParams)
        {
            bool missingParams = false;

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

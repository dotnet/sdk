using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using System.IO;
using System.Diagnostics;
using Microsoft.TemplateEngine.Edge.Runner;

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

        public static async Task<int> Instantiate(CommandLineApplication app, string templateName, CommandOption name, CommandOption dir, CommandOption help, CommandOption alias, IReadOnlyDictionary<string, string> inputParameters, bool quiet, bool skipUpdateCheck)
        {
            if (string.IsNullOrWhiteSpace(templateName) && help.HasValue())
            {
                app.ShowHelp();
                return 0;
            }

            ITemplateInfo tmpltInfo;

            using (Timing.Over("Get single"))
            {
                if (! TryGetTemplate(templateName, out tmpltInfo))
                {
                    return -1;
                }
            }

            ITemplate template = SettingsLoader.LoadTemplate(tmpltInfo);

            if (!skipUpdateCheck)
            {
                if (!quiet)
                {
                    Reporter.Output.WriteLine("Checking for updates...");
                }

                //UpdateCheck();    // this'll need params
            }

            string realName = name.Value() ?? template.DefaultName ?? new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            IParameterSet templateParams = SetupDefaultParamValuesFromTemplate(template, realName);

            if (alias.HasValue())
            {
                //TODO: Add parameters to aliases (from _parameters_ collection)
                AliasRegistry.SetTemplateAlias(alias.Value(), template);
                Reporter.Output.WriteLine("Alias created.");
                return 0;
            }

            OverrideTemplateParameters(templateParams, inputParameters);
            bool missingProps = CheckForMissingRequiredParameters(help, templateParams);

            if (help.HasValue() || missingProps)
            {
                DisplayParameterProblems(template);
                return missingProps ? -1 : 0;
            }

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                await template.Generator.Create(new Orchestrator(), template, templateParams);
                sw.Stop();

                if (!quiet)
                {
                    Reporter.Output.WriteLine($"Content generated in {sw.Elapsed.TotalMilliseconds} ms");
                }
            }
            finally
            {
                if (dir.HasValue())
                {
                    Directory.SetCurrentDirectory(Directory.CreateDirectory(realName).FullName);
                }

                string currentDir = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(currentDir);
            }

            return 0;
        }

        // 
        // Reads the parameters from the template and setup their values in the return IParameterSet.
        //
        public static IParameterSet SetupDefaultParamValuesFromTemplate(ITemplate tmplt, string realName)
        {
            IParameterSet templateParams = tmplt.Generator.GetParametersForTemplate(tmplt);

            foreach (ITemplateParameter param in templateParams.ParameterDefinitions)
            {
                if (param.IsName)
                {
                    templateParams.ResolvedValues[param] = realName;
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
        public static void OverrideTemplateParameters(IParameterSet templateParams, IReadOnlyDictionary<string, string> inputParameters)
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

        public static bool CheckForMissingRequiredParameters(CommandOption help, IParameterSet templateParams)
        {
            bool missingProps = false;

            foreach (ITemplateParameter parameter in templateParams.ParameterDefinitions)
            {
                if (!help.HasValue() && parameter.Priority == TemplateParameterPriority.Required && !templateParams.ResolvedValues.ContainsKey(parameter))
                {
                    Reporter.Error.WriteLine($"Missing required parameter {parameter.Name}".Bold().Red());
                    missingProps = true;
                }
            }

            return missingProps;
        }

        public static void DisplayParameterProblems(ITemplate template)
        {
            string val;
            if (template.TryGetProperty("Description", out val))
            {
                Reporter.Output.WriteLine($"{val}");
            }

            if (template.TryGetProperty("Author", out val))
            {
                Reporter.Output.WriteLine($"Author: {val}");
            }

            if (template.TryGetProperty("DiskPath", out val))
            {
                Reporter.Output.WriteLine($"Disk Path: {val}");
            }

            Reporter.Output.WriteLine("Parameters:");
            foreach (ITemplateParameter parameter in template.Generator.GetParametersForTemplate(template).ParameterDefinitions.OrderBy(x => x.Priority).ThenBy(x => x.Name))
            {
                Reporter.Output.WriteLine(
                    $@"    {parameter.Name} ({parameter.Priority})
        Type: {parameter.Type}");

                if (!string.IsNullOrEmpty(parameter.Documentation))
                {
                    Reporter.Output.WriteLine($"        Documentation: {parameter.Documentation}");
                }

                if (!string.IsNullOrEmpty(parameter.DefaultValue))
                {
                    Reporter.Output.WriteLine($"        Default: {parameter.DefaultValue}");
                }
            }
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

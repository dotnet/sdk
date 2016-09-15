using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class FlagsConfig : IOperationConfig
    {
        public int Order => 9000;

        public string Key => "flags";

        public Guid Id => new Guid("A1E27A4B-9608-47F1-B3B8-F70DF62DC521");

        public IEnumerable<IOperationProvider> Process(IComponentManager componentManager, JObject rawConfiguration, IDirectory templateRoot, IVariableCollection variables, IParameterSet parameters)
        {
            foreach (JProperty property in rawConfiguration.Properties())
            {
                JObject innerData = (JObject)property.Value;
                string flag = property.Name;
                string on = innerData.ToString("on") ?? string.Empty;
                string off = innerData.ToString("off") ?? string.Empty;
                string onNoEmit = innerData.ToString("onNoEmit") ?? string.Empty;
                string offNoEmit = innerData.ToString("offNoEmit") ?? string.Empty;
                string defaultStr = innerData.ToString("default");
                string id = innerData.ToString("id");
                bool? @default = null;

                if (defaultStr != null)
                {
                    @default = bool.Parse(defaultStr);
                }

                yield return new SetFlag(flag, on, off, onNoEmit, offNoEmit, id, @default);
            }
        }

        public static readonly string FlagConditionalSuffix = ":cnd";
        public static readonly string FlagReplacementSuffix = ":replacements";
        public static readonly string FlagExpandVariablesSuffix = ":vars";
        public static readonly string FlagIncludeSuffix = ":include";
        public static readonly string FlagFlagsSuffix = ":flags";

        // Returns a default flags operations setup for the given switchPrefix
        public static IReadOnlyList<IOperationProvider> FlagsDefaultSetup(string switchPrefix)
        {
            List<IOperationProvider> flagOperations = new List<IOperationProvider>();
            string on = string.Format("{0}+{1}", switchPrefix, FlagConditionalSuffix);
            string off = string.Format("{0}-{1}", switchPrefix, FlagConditionalSuffix);
            flagOperations.Add(new SetFlag("conditionals", on, off, string.Empty, string.Empty, string.Empty));

            on = string.Format("{0}+{1}", switchPrefix, FlagReplacementSuffix);
            off = string.Format("{0}-{1}", switchPrefix, FlagReplacementSuffix);
            flagOperations.Add(new SetFlag("replacements", on, off, string.Empty, string.Empty, string.Empty));

            on = string.Format("{0}+{1}", switchPrefix, FlagExpandVariablesSuffix);
            off = string.Format("{0}-{1}", switchPrefix, FlagExpandVariablesSuffix);
            flagOperations.Add(new SetFlag("expandVariables", on, off, string.Empty, string.Empty, string.Empty));

            on = string.Format("{0}+{1}", switchPrefix, FlagIncludeSuffix);
            off = string.Format("{0}-{1}", switchPrefix, FlagIncludeSuffix);
            flagOperations.Add(new SetFlag("include", on, off, string.Empty, string.Empty, string.Empty));

            on = string.Format("{0}+{1}", switchPrefix, FlagFlagsSuffix);
            flagOperations.Add(new SetFlag("flags", on, off, string.Empty, string.Empty, string.Empty));

            return flagOperations;
        }
    }
}
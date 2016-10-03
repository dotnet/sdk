using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public static class LocalizationConfig
    {
        public static List<IOperationProvider> FromJObject(JObject rawConfig)
        {
            List<IOperationProvider> replacements = new List<IOperationProvider>();

            foreach (JObject entry in rawConfig.Items<JObject>("translations"))
            {
                string original = entry.ToString("original");
                string translation = entry.ToString("translation");
                replacements.Add(new Replacement(original, translation, null));
            }

            return replacements;
        }
    }
}

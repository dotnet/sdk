using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class RunnableProjectConfigConverter
    {
        public static IRunnableProjectConfig FromJObject(JObject o)
        {
            JToken extendedConfigToken;
            if (o.TryGetValue("config", StringComparison.OrdinalIgnoreCase, out extendedConfigToken))
            {
                ConfigModel.FromJObject(o);
            }

            return SimpleConfigModel.FromJObject(o);
        }
    }
}
using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    public class BalancedNestingConfig : IOperationConfig
    {
        public int Order => 11000;

        public string Key => "balancednestings";

        public Guid Id => new Guid("3147965A-08E5-4523-B869-02C8E9A8AAA1");

        public IEnumerable<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration, IDirectory templateRoot)
        {
            string startToken = rawConfiguration.ToString("startToken");
            string realEndToken = rawConfiguration.ToString("realEndToken");
            string pseudoEndToken = rawConfiguration.ToString("pseudoEndToken");
            string id = rawConfiguration.ToString("id");
            string resetFlag = rawConfiguration.ToString("resetFlag");

            yield return new BalancedNesting(startToken, realEndToken, pseudoEndToken, id, resetFlag);
        }

        public static JObject CreateConfiguration(string startToken, string realEndToken, string pseudoEndToken, string id, string resetFlag)
        {
            JObject config = new JObject();
            config["startToken"] = startToken;
            config["realEndToken"] = realEndToken;
            config["pseudoEndToken"] = pseudoEndToken;
            config["id"] = id;
            config["resetFlag"] = resetFlag;

            return config;
        }
    }
}

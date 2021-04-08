using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config
{
    internal class BalancedNestingConfig : IOperationConfig
    {
        public string Key => BalancedNesting.OperationName;

        public Guid Id => new Guid("3147965A-08E5-4523-B869-02C8E9A8AAA1");

        public IEnumerable<IOperationProvider> ConfigureFromJObject(JObject rawConfiguration, IDirectory templateRoot)
        {
            string startToken = rawConfiguration.ToString("startToken");
            string realEndToken = rawConfiguration.ToString("realEndToken");
            string pseudoEndToken = rawConfiguration.ToString("pseudoEndToken");
            string id = rawConfiguration.ToString("id");
            string resetFlag = rawConfiguration.ToString("resetFlag");
            bool onByDefault = rawConfiguration.ToBool("onByDefault");

            yield return new BalancedNesting(startToken.TokenConfig(), realEndToken.TokenConfig(), pseudoEndToken.TokenConfig(), id, resetFlag, onByDefault);
        }

        internal static JObject CreateConfiguration(string startToken, string realEndToken, string pseudoEndToken, string id, string resetFlag)
        {
            JObject config = new JObject
            {
                ["startToken"] = startToken,
                ["realEndToken"] = realEndToken,
                ["pseudoEndToken"] = pseudoEndToken,
                ["id"] = id,
                ["resetFlag"] = resetFlag
            };

            return config;
        }
    }
}

using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    // TODO: make this an IOperationConfig
    public class VariableConfig : IVariableConfig
    {
        public IReadOnlyDictionary<string, string> Sources { get; set; }

        public IReadOnlyList<string> Order { get; set; }

        public string FallbackFormat { get; set; }

        public bool Expand { get; set; }

        public static IVariableConfig DefaultVariableSetup()
        {
            IVariableConfig config = new VariableConfig
            {
                Sources = new Dictionary<string, string>
                {
                    { "environment", "env_{0}" },
                    { "user", "usr_{0}" }
                },
                Order = new List<string>() { "environment", "user" },
                FallbackFormat = "{0}",
                Expand = false
            };

            return config;
        }
    }
}

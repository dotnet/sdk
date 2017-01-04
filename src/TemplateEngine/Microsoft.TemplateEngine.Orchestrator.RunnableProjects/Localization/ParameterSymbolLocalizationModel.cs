using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization
{
    public class ParameterSymbolLocalizationModel : IParameterSymbolLocalizationModel
    {
        public ParameterSymbolLocalizationModel(string name, string description, IReadOnlyDictionary<string, string> choicesAndDescriptions)
        {
            Name = name;
            Description = description;
            ChoicesAndDescriptions = choicesAndDescriptions;
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }
    }
}

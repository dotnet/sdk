using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IParameterSymbolLocalizationModel
    {
        string Name { get; }

        string Description { get; }

        IReadOnlyDictionary<string, string> ChoicesAndDescriptions { get; }
    }
}

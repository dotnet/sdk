using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class SymbolModelConverter
    {
        // Note: Only ParameterSymbol has a Description property, this it's the only one that gets localization
        // TODO: change how localization gets merged in, don't do it here.
        public static ISymbolModel GetModelForObject(JObject jObject, IParameterSymbolLocalizationModel localization, string defaultOverride)
        {
            switch (jObject.ToString(nameof(ISymbolModel.Type)))
            {
                case "parameter":
                    return ParameterSymbol.FromJObject(jObject, localization, defaultOverride);
                case "computed":
                    return ComputedSymbol.FromJObject(jObject);
                case "bind":
                case "generated":
                    return GeneratedSymbol.FromJObject(jObject);
                default:
                    return null;
            }
        }
    }
}

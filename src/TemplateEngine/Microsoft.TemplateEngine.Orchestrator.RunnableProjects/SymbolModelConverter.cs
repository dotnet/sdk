using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class SymbolModelConverter
    {
        // Note: Only ParameterSymbol has a Description property, this it's the only one that gets localizedDescriptipn
        public static ISymbolModel GetModelForObject(JObject jObject, string localizedDescriptipn = null)
        {
            switch (jObject.ToString(nameof(ISymbolModel.Type)))
            {
                case "parameter":
                    return ParameterSymbol.FromJObject(jObject, localizedDescriptipn);
                case "computed":
                    return ComputedSymbol.FromJObject(jObject);
                case "generated":
                    return GeneratedSymbol.FromJObject(jObject);
                default:
                    return null;
            }
        }
    }
}
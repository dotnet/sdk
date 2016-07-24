using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal class SymbolModelConverter
    {
        public static ISymbolModel GetModelForObject(JObject jObject)
        {
            switch (jObject.ToString(nameof(ISymbolModel.Type)))
            {
                case "parameter":
                    return ParameterSymbol.FromJObject(jObject);
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